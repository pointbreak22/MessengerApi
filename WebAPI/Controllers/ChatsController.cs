using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MediatR;
using Application.CQRS.Groups.Commands;
using Application.CQRS.Chats.Commands;
using Application.CQRS.Chats.Queries;
using Application.CQRS.Users.Queries;

namespace WebAPI.Controllers
{
    public class ChatsController : ApiControllerBase
    {
        private static readonly string[] AllowedAvatarTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];
        private const long MaxAvatarSizeBytes = 5 * 1024 * 1024; // 5 MB

        private readonly IMediator _mediator;
        private readonly BlobServiceClient? _blob;

        public ChatsController(IMediator mediator, BlobServiceClient? blob = null)
        {
            _mediator = mediator;
            _blob = blob;
        }

        [HttpPost("group")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Group name required.");

            var chatId = await _mediator.Send(new CreateGroupCommand(dto.Name, userId, dto.MemberIds, dto.IsPublic));
            return Ok(new { ChatId = chatId });
        }

        [HttpGet("public")]
        public async Task<IActionResult> GetPublicGroups(
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var (items, total) = await _mediator.Send(new GetPublicGroupsQuery(page, pageSize, search, userId));
            return Ok(new { items, total, page, pageSize });
        }

        [HttpPost("{chatId}/join")]
        public async Task<IActionResult> Join(Guid chatId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                await _mediator.Send(new JoinGroupCommand(chatId, userId));
                return Ok();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        [HttpPost("direct/{targetUserId}")]
        public async Task<IActionResult> CreateDirect(string targetUserId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (userId == targetUserId) return BadRequest("Cannot create a chat with yourself.");

            var target = await _mediator.Send(new GetUserByIdQuery(targetUserId));
            if (target == null) return NotFound("User not found.");

            var chatId = await _mediator.Send(new CreateDirectChatCommand(userId, targetUserId));
            return Ok(new { ChatId = chatId });
        }

        [HttpPost("{chatId}/members")]
        public async Task<IActionResult> AddMember(Guid chatId, [FromBody] AddMemberDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                await _mediator.Send(new AddMemberCommand(chatId, dto.UserId, userId));
                return Ok();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Owner-only, enforced here — not just hidden in the UI.
        [HttpDelete("{chatId}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(Guid chatId, string userId)
        {
            var requesterId = GetCurrentUserId();
            if (string.IsNullOrEmpty(requesterId)) return Unauthorized();

            try
            {
                await _mediator.Send(new RemoveMemberCommand(chatId, userId, requesterId));
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        // Owner-only, same caveat as RemoveMember.
        [HttpDelete("{chatId}")]
        public async Task<IActionResult> DeleteChat(Guid chatId)
        {
            var requesterId = GetCurrentUserId();
            if (string.IsNullOrEmpty(requesterId)) return Unauthorized();

            try
            {
                await _mediator.Send(new DeleteChatCommand(chatId, requesterId));
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        // Self-service leave — any member, including one who isn't the owner.
        // Distinct from DELETE /members/{userId}, which is owner-only and targets
        // someone else.
        [HttpPost("{chatId}/leave")]
        public async Task<IActionResult> Leave(Guid chatId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                await _mediator.Send(new LeaveChatCommand(chatId, userId));
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> MyChats()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var chats = await _mediator.Send(new GetMyChatsQuery(userId));
            return Ok(chats);
        }

        // Any member can rename a group, not just the owner — same invite
        // model as AddMember.
        [HttpPut("{chatId}")]
        public async Task<IActionResult> Rename(Guid chatId, [FromBody] RenameChatDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required.");

            try
            {
                await _mediator.Send(new RenameChatCommand(chatId, userId, dto.Name));
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{chatId}/avatar")]
        public async Task<IActionResult> UpdateAvatar(Guid chatId, [FromForm] IFormFile file)
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
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
            // "group-" prefix keeps these apart from user avatars in the same
            // container/naming scheme (blob name is otherwise just an id + ext).
            var blob = container.GetBlobClient($"group-{chatId}{ext}");
            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType }, conditions: null);

            var avatarUrl = blob.Uri.ToString();
            try
            {
                await _mediator.Send(new UpdateChatAvatarCommand(chatId, userId, avatarUrl));
                return Ok(new { avatarUrl });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{chatId}/avatar")]
        public async Task<IActionResult> DeleteAvatar(Guid chatId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (_blob != null)
            {
                var container = _blob.GetBlobContainerClient("avatars");
                foreach (var ext in new[] { ".jpg", ".png", ".gif", ".webp" })
                    await container.GetBlobClient($"group-{chatId}{ext}").DeleteIfExistsAsync();
            }

            try
            {
                await _mediator.Send(new UpdateChatAvatarCommand(chatId, userId, null));
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    public record CreateGroupDto(string Name, string[]? MemberIds, bool IsPublic = false);
    public record AddMemberDto(string UserId);
    public record RenameChatDto(string Name);
}
