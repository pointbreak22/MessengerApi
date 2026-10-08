// This is a placeholder for the actual content of DependencyInjection.cs
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Domain.Repositories;
using Infrastructure.Repositories;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b =>
                    {
                        b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);

                        // Раньше стояло 5x10с — при подвисании коннекта к пулеру Npgsql это
                        // растягивалось до ~2 минут суммарно и Azure убивал весь w3wp как зависший,
                        // роняя заодно все остальные запросы. Короткий retry укладывается в секунды.
                        b.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(3),
                            errorCodesToAdd: null);

                        b.CommandTimeout(30);
                    })
            );

            services.AddScoped<IUserRepository, EfUserRepository>();
            services.AddScoped<IChatRepository, EfChatRepository>();
            services.AddScoped<IMessageRepository, EfMessageRepository>();
            services.AddScoped<IMessageReactionRepository, EfMessageReactionRepository>();
            services.AddScoped<IOutboxRepository, EfOutboxRepository>();
            services.AddScoped<IFriendshipRepository, EfFriendshipRepository>();
            services.AddScoped<IModerationRepository, EfModerationRepository>();
            services.AddScoped<IIpBanRepository, EfIpBanRepository>();

            return services;
        }
    }
}