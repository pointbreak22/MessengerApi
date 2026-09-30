using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using WebAPI.Auth;
using WebAPI.Hubs;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    /// <summary>
    /// Панель суперадмина. Политика навешена на класс, а не на отдельные методы —
    /// так новый endpoint нельзя случайно оставить открытым.
    ///
    /// Работает с репозиториями напрямую, минуя команды из Application. Это
    /// сознательно: DeleteChatCommand и RenameChatCommand проверяют, что запросивший
    /// — владелец чата, а смысл админки ровно в обходе этого правила. Добавлять в них
    /// флаг "а этому можно" пришлось бы в каждую команду, и любая забытая проверка
    /// превращалась бы в дыру. Отдельная поверхность с одной политикой на входе
    /// понятнее и безопаснее.
    /// </summary>
    [Authorize(Policy = SuperAdminRequirement.PolicyName)]
    [Route("api/admin")]
    public class AdminController : ApiControllerBase
    {
        private const int MaxPageSize = 200;

        private readonly IUserRepository _users;
        private readonly IChatRepository _chats;
        private readonly BanStatusService _bans;
        private readonly SuperAdminPolicy _superAdmin;
        private readonly IHubContext<ChatHub> _hub;

        public AdminController(IUserRepository users, IChatRepository chats, BanStatusService bans, SuperAdminPolicy superAdmin, IHubContext<ChatHub> hub)
        {
            _users = users;
            _chats = chats;
            _bans = bans;
            _superAdmin = superAdmin;
            _hub = hub;
        }

        public record AdminUserDto(string Id, string UserName, string? Email, UserRole Role, bool IsOnline, DateTime LastSeenAt, string? AvatarUrl, bool IsBanned, DateTime? BannedAt)
        {
            public static AdminUserDto FromEntity(User u) =>
                new(u.Id, u.UserName, u.Email, u.Role, u.IsOnline, u.LastSeenAt, u.AvatarUrl, u.IsBanned, u.BannedAt);
        }
        public record AdminChatDto(Guid Id, string? Name, string? AvatarUrl, bool IsPublic, string? OwnerId, DateTime CreatedAt, int MemberCount);
        public record RenameUserDto(string UserName);
        public record UpdateChatDto(string? Name, bool? IsPublic);

        /// <summary>Все, кто когда-либо входил: строка в БД создаётся при первом входе.</summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
        {
            var (items, total) = await _users.GetPagedAsync(Normalize(page), Normalize(pageSize, MaxPageSize), search, includeBanned: true);
            var users = items.Select(AdminUserDto.FromEntity);
            return Ok(new { items = users, total });
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> RenameUser(string id, [FromBody] RenameUserDto dto)
        {
            var name = dto?.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Имя не может быть пустым.");

            var user = await _users.GetByIdAsync(id);
            if (user == null) return NotFound();

            user.UpdateProfile(name);
            await _users.UpdateAsync(user);
            return Ok(AdminUserDto.FromEntity(user));
        }

        /// <summary>
        /// Бан: пользователь больше не может ничего делать (middleware и фильтр хаба
        /// отвечают ему 403 / account_banned) и пропадает из поиска, друзей, чатов и
        /// сообщений у остальных. Данные не удаляются — разбан всё возвращает.
        /// </summary>
        [HttpPost("users/{id}/ban")]
        public async Task<IActionResult> BanUser(string id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null) return NotFound();

            if (id == GetCurrentUserId()) return BadRequest("Нельзя заблокировать самого себя.");
            if (user.Role == UserRole.SuperAdmin || _superAdmin.IsSuperAdminEmail(user.Email))
                return BadRequest("Нельзя заблокировать суперадмина.");

            user.Ban();
            await _users.UpdateAsync(user);
            _bans.Invalidate(id);

            // Открытая вкладка забаненного узнаёт о бане сразу и закрывает соединение;
            // остальные видят его офлайн. Методы хаба ему уже недоступны (BanHubFilter).
            await _hub.Clients.User(id).SendAsync("AccountBanned");
            await _hub.Clients.All.SendAsync("UserWentOffline", id);

            return Ok(AdminUserDto.FromEntity(user));
        }

        [HttpPost("users/{id}/unban")]
        public async Task<IActionResult> UnbanUser(string id)
        {
            var user = await _users.GetByIdAsync(id);
            if (user == null) return NotFound();

            user.Unban();
            await _users.UpdateAsync(user);
            _bans.Invalidate(id);

            return Ok(AdminUserDto.FromEntity(user));
        }

        [HttpGet("chats")]
        public async Task<IActionResult> GetPublicChats([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
        {
            var (items, total) = await _chats.GetPublicGroupsAsync(Normalize(page), Normalize(pageSize, MaxPageSize), search);
            var chats = items.Select(c => new AdminChatDto(c.Id, c.Name, c.AvatarUrl, c.IsPublic, c.OwnerId, c.CreatedAt, c.Members?.Count ?? 0));
            return Ok(new { items = chats, total });
        }

        [HttpPut("chats/{id:guid}")]
        public async Task<IActionResult> UpdateChat(Guid id, [FromBody] UpdateChatDto dto)
        {
            var chat = await _chats.GetByIdAsync(id);
            if (chat == null) return NotFound();

            if (dto?.Name != null)
            {
                var name = dto.Name.Trim();
                if (string.IsNullOrWhiteSpace(name)) return BadRequest("Название не может быть пустым.");
                chat.Rename(name);
            }

            if (dto?.IsPublic != null) chat.SetPublic(dto.IsPublic.Value);

            await _chats.UpdateAsync(chat);
            return Ok(new AdminChatDto(chat.Id, chat.Name, chat.AvatarUrl, chat.IsPublic, chat.OwnerId, chat.CreatedAt, chat.Members?.Count ?? 0));
        }

        [HttpDelete("chats/{id:guid}")]
        public async Task<IActionResult> DeleteChat(Guid id)
        {
            var chat = await _chats.GetByIdAsync(id);
            if (chat == null) return NotFound();

            await _chats.DeleteAsync(chat);
            return NoContent();
        }

        private static int Normalize(int value, int max = int.MaxValue) => Math.Clamp(value, 1, max);
    }
}
