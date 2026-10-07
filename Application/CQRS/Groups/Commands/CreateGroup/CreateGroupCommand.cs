using System;
using System.Collections.Generic;
using MediatR;
using Application.Moderation;
using Domain.Enums;

namespace Application.CQRS.Groups.Commands
{
    public record CreateGroupCommand(string Name, string OwnerId, string[]? MemberIds, bool IsPublic = false) : IRequest<Guid>, IModeratedRequest
    {
        string IModeratedRequest.ModerationActorId => OwnerId;
        IEnumerable<ModeratedText> IModeratedRequest.GetModeratedTexts() => new[] { new ModeratedText(ModerationTarget.ChatName, Name, "создание группы") };
    }
}
