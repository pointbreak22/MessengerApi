namespace WebAPI.Services
{
    public class NullPresenceService : IPresenceService
    {
        public Task AddConnectionAsync(string userId, string connectionId) => Task.CompletedTask;

        public Task<bool> RemoveConnectionAsync(string userId, string connectionId) => Task.FromResult(true);

        public Task<IReadOnlyCollection<string>> GetConnectionsAsync(string userId) =>
            Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
    }
}
