using WebAPI.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Extensions.Hosting;
using Azure.Identity;
using StackExchange.Redis;
using Scalar.AspNetCore;



var builder = WebApplication.CreateBuilder(args);

// Секреты (ключи хранилищ и т.п.) лежат в appsettings.Secrets.json: он не в git
// (репозиторий публичный), но публикуется вместе с приложением. Переменные окружения
// App Service по-прежнему имеют приоритет, поэтому добавляем их заново поверх файла.
builder.Configuration
    .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Опционально: подключение Azure Key Vault если настроено (см. notes)
if (!string.IsNullOrEmpty(builder.Configuration["KeyVault:Endpoint"]))
{
    try
    {
        var kvEndpoint = new Uri(builder.Configuration["KeyVault:Endpoint"]!);
        builder.Configuration.AddAzureKeyVault(kvEndpoint, new Azure.Identity.DefaultAzureCredential());
    }
    catch
    {
        // Если KeyVault не доступен в локальной среде, продолжаем без ошибки
    }
}

// 1. Подключаем контроллеры
builder.Services.AddControllers();

// CORS — allowed origins configured in appsettings ("AllowedOrigins" array)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:4200"];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Serilog
try
{
    Serilog.Log.Logger = new Serilog.LoggerConfiguration()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .CreateLogger();
    builder.Host.UseSerilog();
}
catch { }

// 2. OpenAPI — документ + JWT Bearer security scheme, авто-снятие требования авторизации с [AllowAnonymous]
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT, полученный через Authentication:Authority (Microsoft Entra External ID)."
        };
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var hasAllowAnonymous = context.Description.ActionDescriptor is ControllerActionDescriptor actionDescriptor
            && (actionDescriptor.MethodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any()
                || actionDescriptor.ControllerTypeInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any());

        if (hasAllowAnonymous)
        {
            operation.Security?.Clear();
        }
        else
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", hostDocument: null, externalResource: null)] = []
            });
        }

        return Task.CompletedTask;
    });
});

// Application Insights (если ключ присутствует в конфигурации)
if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:InstrumentationKey"]))
{
    // AddApplicationInsightsTelemetry will read instrumentation key from configuration automatically
    builder.Services.AddApplicationInsightsTelemetry();
}

// OpenTelemetry registration (OTLP exporter) if configured
// OpenTelemetry: for production enable OTLP or Application Insights exporter.
// Example setup is provided in docs/OPEN_TELEMETRY.md. Package availability may vary between environments.

// ==================== ЧТО МЫ ДОБАВИЛИ ====================

// 3. Подключаем базу данных и репозитории из слоя Infrastructure
Infrastructure.DependencyInjection.AddInfrastructure(builder.Services, builder.Configuration);

// 4. Регистрируем MediatR (указываем ему искать команды/хэндлеры в слое Application)
// Используем typeof(Handler).Assembly чтобы надёжно указать сборку с хэндлерами
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Application.CQRS.Messages.Commands.SendMessageCommandHandler).Assembly));
// 5. Настраиваем Аутентификацию через Microsoft.Identity.Web (Entra External ID / CIAM).
// Instance/TenantId/ClientId читаются из секции "AzureAd" в appsettings — пакет сам разруливает
// нюансы CIAM (JWKS, issuer-алиасы), которых не хватало при ручной настройке TokenValidationParameters.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(jwtOptions =>
    {
        jwtOptions.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "Authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    }, identityOptions => builder.Configuration.Bind("AzureAd", identityOptions));
// 6. Подключаем SignalR
// Регистрация провайдера UserId для SignalR (берёт id из claim 'sub')
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, WebAPI.Services.CustomUserIdProvider>();

// Роли и админка. SuperAdminHandler — Scoped, а не Singleton по умолчанию: он
// обращается к IUserRepository, который тоже Scoped.
builder.Services.AddSingleton<WebAPI.Services.SuperAdminPolicy>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, WebAPI.Auth.SuperAdminHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(WebAPI.Auth.SuperAdminRequirement.PolicyName, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new WebAPI.Auth.SuperAdminRequirement());
    });
});
// Redis (StackExchange) cache для presence, если указан connection string
var redisConn = builder.Configuration["Redis:Configuration"] ?? builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConn))
{
    builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisConn; });
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
        StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
    builder.Services.AddSingleton<WebAPI.Services.IPresenceService, WebAPI.Services.PresenceService>();
    // Group-call room membership (presence badges, "join ongoing call") — same
    // Redis connection as presence above.
    builder.Services.AddSingleton<WebAPI.Services.IActiveCallService, WebAPI.Services.RedisActiveCallService>();
}
else
{
    builder.Services.AddSingleton<WebAPI.Services.IPresenceService, WebAPI.Services.NullPresenceService>();
    // Unlike NullPresenceService, this fallback is actually functional
    // in-memory — group-call presence needs *some* shared state to do
    // anything at all, and in-memory is correct for a single-instance deploy.
    builder.Services.AddSingleton<WebAPI.Services.IActiveCallService, WebAPI.Services.InMemoryActiveCallService>();
}

// Бан пользователей: кэш статуса + проверка в middleware (REST, negotiate) и в фильтре хаба.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<WebAPI.Services.BanStatusService>();

Action<Microsoft.AspNetCore.SignalR.HubOptions> configureHub = options =>
    Microsoft.AspNetCore.SignalR.HubOptionsExtensions.AddFilter<WebAPI.Auth.BanHubFilter>(options);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSignalR(configureHub); // Локально используем обычный SignalR
}
else
{
    // В Azure подключаем специальный сервис
    builder.Services.AddSignalR(configureHub).AddAzureSignalR(options =>
    {
        options.ConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"];
    });
}

// Outbox dispatcher
// Single shared instance: message handlers resolve it through IOutboxSignal to
// signal, OutboxDispatcher takes the concrete type to wait on it.
builder.Services.AddSingleton<WebAPI.Services.OutboxSignal>();
builder.Services.AddSingleton<Domain.Repositories.IOutboxSignal>(sp => sp.GetRequiredService<WebAPI.Services.OutboxSignal>());
builder.Services.AddHostedService<WebAPI.Services.OutboxDispatcher>();

// Realtime notifier implementation
builder.Services.AddSingleton<Application.Common.IRealtimeNotifier, WebAPI.Services.RealtimeNotifier>();

// Blob storage client
var blobConn = builder.Configuration["Azure:Blob:ConnectionString"];
if (!string.IsNullOrEmpty(blobConn))
{
    try
    {
        builder.Services.AddSingleton(new Azure.Storage.Blobs.BlobServiceClient(blobConn));
    }
    catch (Exception ex)
    {
        // Некорректная connection string не должна ронять весь процесс на старте —
        // без неё эндпоинты аватарок/вложений вернут 503, остальной API продолжит работать.
        Serilog.Log.Logger.Error(ex, "Не удалось создать BlobServiceClient — проверьте Azure:Blob:ConnectionString.");
    }
}

// ========================================================

var app = builder.Build();

// Применяем все накопленные миграции при старте — если таблиц ещё нет, EF Core их создаст.
// Ошибку не пробрасываем дальше: недоступность БД (firewall, maintenance и т.п.) не должна ронять
// весь процесс — иначе недоступны даже /scalar и health-эндпоинты. Причина попадёт в лог.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
    try
    {
      //  db.Database.Migrate();
    }
    catch (Exception ex)
    {
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
            .LogError(ex, "Не удалось применить миграции EF Core при старте приложения. Запросы к БД будут падать, пока проблема не будет устранена.");
    }
}

// Создаём нужные blob-контейнеры (avatars, attachments), если их ещё нет, и
// принудительно выставляем им публичный доступ на чтение blob'ов — не только
// при создании: CreateIfNotExistsAsync задаёт уровень доступа лишь в момент
// создания, так что контейнер, заведённый вручную через портал (или созданный
// старой версией кода с другим PublicAccessType), тут не поправится сам без
// явного SetAccessPolicyAsync. Как и миграции выше — не фатально: если Blob
// недоступен/не настроен, остальной API продолжает работать, эндпоинты
// аватарок/вложений вернут 503.
using (var scope = app.Services.CreateScope())
{
    var blob = scope.ServiceProvider.GetService<Azure.Storage.Blobs.BlobServiceClient>();
    if (blob != null)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        foreach (var containerName in new[] { "avatars", "attachments" })
        {
            try
            {
                var container = blob.GetBlobContainerClient(containerName);
                await container.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);
                await container.SetAccessPolicyAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не удалось подготовить blob-контейнер '{Container}'.", containerName);
            }
        }
    }
}

// Configure the HTTP request pipeline.
// OpenAPI-документ и Scalar UI доступны во всех окружениях (тестовый деплой) — сам API остаётся под JwtBearer, эндпоинты доков не защищены.
app.MapOpenApi();
app.MapScalarApiReference(); // UI: /scalar/v1
app.MapGet("/", () => Results.Redirect("/scalar/v1")); // корень сразу ведёт на доки API

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseMiddleware<WebAPI.Auth.BanEnforcementMiddleware>();
app.UseAuthorization();

app.MapControllers();

// 7. Маппим SignalR Хаб на эндпоинт
app.MapHub<ChatHub>("/chathub");

app.Run();