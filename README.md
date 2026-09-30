# MessengerApi

Backend of a real-time messenger: ASP.NET Core Web API + SignalR hub, deployed on Azure.
Frontend: [MessengerClient](https://github.com/pointbreak22/MessengerClient) · Demo: https://green-mud-0a6cd920f.7.azurestaticapps.net/app

## Stack

- .NET 10, ASP.NET Core, Clean Architecture (Domain → Application → Infrastructure → WebAPI), CQRS with MediatR
- EF Core + PostgreSQL, Redis (presence, group call rooms)
- SignalR (Azure SignalR Service in production): messages, typing, presence, 1:1 and group WebRTC call signaling
- Outbox pattern with a background dispatcher, idempotent message sending
- Authentication: Microsoft Entra External ID (JWT), role-based admin panel (superadmin, account bans)
- Azure: App Service, Blob Storage (avatars, attachments), Key Vault, Application Insights, OpenTelemetry

## Running locally

1. Set `ConnectionStrings:DefaultConnection` (PostgreSQL) and the `AzureAd` section.
2. Put secrets (e.g. `Azure:Blob:ConnectionString`) into `WebAPI/appsettings.Secrets.json`,
   user secrets or environment variables — that file is git-ignored.
3. Apply migrations: `dotnet ef database update --project Infrastructure --startup-project WebAPI`
4. `dotnet run --project WebAPI` → API docs at `/scalar/v1`.

Tests: `dotnet test Tests`.
