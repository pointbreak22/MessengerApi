using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Application.CQRS.Messages.Commands;
using Application.CQRS.Messages.Queries;
using Application.CQRS.Chats.Queries;
using Application.CQRS.Chats.Commands;
using MediatR;

namespace WebAPI.Controllers
{
    public class MessagesController : ApiControllerBase
    {
        private readonly IMediator _mediator;

        public MessagesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // Полнотекстовый поиск по сообщениям во всех чатах текущего пользователя.
        // Должен быть объявлен раньше {chatId:guid} по тем же причинам, что и обычно —
        // но literal-сегмент "search" в любом случае приоритетнее параметра при выборе маршрута.
        [HttpGet("search")]
        public async Task<IActionResult> SearchMessages([FromQuery] string q, [FromQuery] int limit = 20)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<object>());
            if (limit is < 1 or > 50) limit = 20;

            var results = await _mediator.Send(new SearchMessagesQuery(userId, q, limit));
            return Ok(results);
        }

        // История сообщений. Пагинация курсором: передай before=createdAt самого старого из
        // уже загруженных сообщений чтобы получить следующую страницу.
        [HttpGet("{chatId:guid}")]
        public async Task<IActionResult> GetMessages(
            Guid chatId,
            [FromQuery] int limit = 50,
            [FromQuery] DateTime? before = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (!await _mediator.Send(new IsChatMemberQuery(chatId, userId))) return Forbid();
            if (limit is < 1 or > 100) limit = 50;

            var messages = await _mediator.Send(new GetMessagesQuery(chatId, limit, before));
            return Ok(messages);
        }

        // Отправить сообщение. Поддерживает текст и/или вложение.
        [HttpPost("{chatId}")]
        public async Task<IActionResult> SendMessage(Guid chatId, [FromBody] SendMessageDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (!await _mediator.Send(new IsChatMemberQuery(chatId, userId))) return Forbid();

            if (string.IsNullOrWhiteSpace(dto.Text) && string.IsNullOrWhiteSpace(dto.AttachmentUrl))
                return BadRequest("Сообщение должно содержать текст или вложение.");

            var messageId = await _mediator.Send(new SendMessageCommand(
                chatId,
                userId,
                dto.Text ?? string.Empty,
                dto.IdempotencyKey,
                dto.AttachmentUrl,
                dto.ReplyToMessageId));

            return Ok(new { messageId });
        }

        // Пометить чат как прочитанный. Вызывать при открытии чата или прокрутке до конца.
        [HttpPost("{chatId}/read")]
        public async Task<IActionResult> MarkRead(Guid chatId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (!await _mediator.Send(new IsChatMemberQuery(chatId, userId))) return Forbid();

            await _mediator.Send(new MarkChatReadCommand(chatId, userId));
            return NoContent();
        }

        // Own messages only — no owner/moderator override, enforced in the handler.
        // Same route template as GetMessages ({chatId:guid}) but a different
        // HTTP verb — ASP.NET Core dispatches on verb + template together, so
        // this doesn't collide with it despite {messageId} being a chat-shaped guid too.
        [HttpPut("{messageId:guid}")]
        public async Task<IActionResult> EditMessage(Guid messageId, [FromBody] EditMessageDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest("Text is required.");

            try
            {
                await _mediator.Send(new EditMessageCommand(messageId, userId, dto.Text));
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
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Toggle: same emoji again removes it, a different emoji replaces it —
        // one reaction per user per message, enforced in the handler + DB index.
        [HttpPut("{messageId:guid}/reactions")]
        public async Task<IActionResult> ToggleReaction(Guid messageId, [FromBody] ToggleReactionDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(dto.Emoji)) return BadRequest("Emoji is required.");

            try
            {
                var reactions = await _mediator.Send(new ToggleReactionCommand(messageId, userId, dto.Emoji));
                return Ok(reactions);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{messageId:guid}")]
        public async Task<IActionResult> DeleteMessage(Guid messageId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                await _mediator.Send(new DeleteMessageCommand(messageId, userId));
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
    }

    public record SendMessageDto(string? Text, string? AttachmentUrl, string? IdempotencyKey, Guid? ReplyToMessageId = null);
    public record EditMessageDto(string Text);
    public record ToggleReactionDto(string Emoji);
}
