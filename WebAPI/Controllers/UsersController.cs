using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Application.CQRS.Users.Commands;
using Application.CQRS.Users.Queries;
using MediatR;

namespace WebAPI.Controllers
{
    public class UsersController : ApiControllerBase
    {
        private static readonly string[] AllowedAvatarTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];
        private const long MaxAvatarSizeBytes = 5 * 1024 * 1024; // 5 MB

        private readonly IMediator _mediator;
        private readonly BlobServiceClient? _blob;
        private readonly WebAPI.Services.SuperAdminPolicy _superAdmin;

        public UsersController(IMediator mediator, WebAPI.Services.SuperAdminPolicy superAdmin, BlobServiceClient? blob = null)
        {
            _mediator   = mediator;
            _superAdmin = superAdmin;
            _blob       = blob;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool? isOnline = null)
        {
            var (items, total) = await _mediator.Send(new GetUsersQuery(page, pageSize, search, sortBy, isOnline));
            return Ok(new { items, total, page, pageSize });
        }

        // Пакетный запрос — получить нескольких пользователей за один запрос.
        // Используется для отображения участников чата без N запросов.
        [HttpPost("batch")]
        public async Task<IActionResult> GetBatch([FromBody] BatchUsersDto dto)
        {
            if (dto.Ids == null || dto.Ids.Length == 0)
                return BadRequest("Ids required.");
            if (dto.Ids.Length > 100)
                return BadRequest("Max 100 ids per request.");

            var users = await _mediator.Send(new GetUsersByIdsQuery(dto.Ids));
            return Ok(users);
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Первый вход этим CIAM-аккаунтом: строки в БД ещё нет — создаём её по claims из токена
            // вместо 404, чтобы фронтенду не нужен был отдельный endpoint регистрации.
            //
            // Здесь же назначается роль: клиент вызывает этот endpoint сразу после входа,
            // так что почта из конфигурации превращается в роль в БД без ручных правок.
            var email = GetCurrentUserEmail();
            var user = await _mediator.Send(
                new EnsureUserCommand(userId, GetCurrentUserName(), email, _superAdmin.IsSuperAdminEmail(email)));
            return Ok(user);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _mediator.Send(new GetUserByIdQuery(id));
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.UserName))
                return BadRequest("UserName is required.");

            await _mediator.Send(new UpdateProfileCommand(userId, dto.UserName));
            return NoContent();
        }

        [HttpPut("me/avatar")]
        public async Task<IActionResult> UpdateAvatar([FromForm] IFormFile file)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("File is empty.");
            if (file.Length > MaxAvatarSizeBytes)
                return BadRequest("Avatar exceeds the 5 MB size limit.");
            if (!AllowedAvatarTypes.Contains(file.ContentType))
                return BadRequest("Only JPEG, PNG, GIF and WebP images are allowed.");
            if (_blob == null)
                return StatusCode(503, "Blob storage is not configured.");

            var container = _blob.GetBlobContainerClient("avatars");
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var ext = file.ContentType switch
            {
                "image/png"  => ".png",
                "image/gif"  => ".gif",
                "image/webp" => ".webp",
                _            => ".jpg"
            };
            var blob = container.GetBlobClient($"{userId}{ext}");
            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType }, conditions: null);

            var avatarUrl = blob.Uri.ToString();
            await _mediator.Send(new UpdateAvatarCommand(userId, avatarUrl));
            return Ok(new { avatarUrl });
        }

        [HttpDelete("me/avatar")]
        public async Task<IActionResult> DeleteAvatar()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (_blob != null)
            {
                var container = _blob.GetBlobContainerClient("avatars");
                foreach (var ext in new[] { ".jpg", ".png", ".gif", ".webp" })
                    await container.GetBlobClient($"{userId}{ext}").DeleteIfExistsAsync();
            }

            await _mediator.Send(new UpdateAvatarCommand(userId, null));
            return NoContent();
        }
    }

    public record BatchUsersDto(string[] Ids);
    public record UpdateProfileDto(string UserName);
}
