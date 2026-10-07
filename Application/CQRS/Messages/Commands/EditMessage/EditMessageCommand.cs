using System;
using System.Collections.Generic;
using MediatR;
using Application.Moderation;
using Domain.Enums;

namespace Application.CQRS.Messages.Commands
{
    public record EditMessageCommand(Guid MessageId, string RequesterId, string Text) : IRequest<MediatR.Unit>, IModeratedRequest
    {
        string IModeratedRequest.ModerationActorId => RequesterId;
        IEnumerable<ModeratedText> IModeratedRequest.GetModeratedTexts() => new[] { new ModeratedText(ModerationTarget.Message, Text, "редактирование сообщения") };
    }
}
