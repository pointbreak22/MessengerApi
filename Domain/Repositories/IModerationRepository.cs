using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IModerationRepository
    {
        // Одним сохранением: изменённый пользователь (страйк/бан), запись о нарушении
        // и уведомления в outbox (письмо, Telegram) — чтобы не вышло бана без письма или письма без страйка.
        Task RecordViolationAsync(User user, ModerationViolation violation, IEnumerable<OutboxMessage> notifications);

        Task<IReadOnlyList<ModerationViolation>> GetByUserAsync(string userId, int take);
    }
}
