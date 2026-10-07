using System;
using System.Threading.Tasks;
using Application.Moderation;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Auth
{
    /// <summary>
    /// Ответ клиенту, когда автомодерация отклонила действие. Одинаковый для REST
    /// (тело 422) и SignalR (событие ContentViolation): клиент показывает message,
    /// а если есть suggestion — предлагает отправить исправленный вариант.
    /// </summary>
    public static class ContentViolationPayload
    {
        public static object From(ContentViolationException ex) => new
        {
            code = ContentViolationException.ErrorCode,
            message = ex.Message,
            target = ex.Target switch
            {
                ModerationTarget.UserName => "userName",
                ModerationTarget.ChatName => "chatName",
                _ => "message",
            },
            matchedWords = ex.MatchedWords,
            suggestion = ex.Suggestion,
            strike = ex.StrikeNumber,
            maxStrikes = ex.MaxStrikes,
            banned = ex.Banned,
        };
    }

    /// <summary>
    /// Глобальный фильтр MVC: ContentViolationException из любой команды → 422,
    /// без try/catch в каждом контроллере.
    /// </summary>
    public sealed class ContentViolationExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not ContentViolationException ex) return;

            context.Result = new ObjectResult(ContentViolationPayload.From(ex))
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity
            };
            context.ExceptionHandled = true;
        }
    }

    /// <summary>
    /// То же для хаба. HubException доносит до клиента только строку, поэтому
    /// подробности (подсказка, номер предупреждения) отправляются вызвавшему
    /// соединению отдельным событием ContentViolation.
    /// </summary>
    public sealed class ContentViolationHubFilter : IHubFilter
    {
        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            try
            {
                return await next(invocationContext);
            }
            catch (ContentViolationException ex)
            {
                try
                {
                    await invocationContext.Hub.Clients.Caller.SendAsync("ContentViolation", ContentViolationPayload.From(ex));
                }
                catch { }
                throw new HubException(ContentViolationException.ErrorCode);
            }
        }
    }
}
