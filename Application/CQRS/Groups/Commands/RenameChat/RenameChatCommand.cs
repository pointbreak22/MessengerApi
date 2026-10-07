using System;
using System.Collections.Generic;
using MediatR;
using Application.Moderation;
using Domain.Enums;

namespace Application.CQRS.Groups.Commands
{
    public record RenameChatCommand(Guid ChatId, string RequesterId, string Name) : IRequest<MediatR.Unit>, IModeratedRequest
    {
        string IModeratedRequest.ModerationActorId => RequesterId;
        IEnumerable<ModeratedText> IModeratedRequest.GetModeratedTexts() => new[] { new ModeratedText(ModerationTarget.ChatName, Name, "переименование чата") };
    }
}
