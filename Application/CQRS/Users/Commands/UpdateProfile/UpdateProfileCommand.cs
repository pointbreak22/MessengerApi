using System.Collections.Generic;
using MediatR;
using Application.Moderation;
using Domain.Enums;

namespace Application.CQRS.Users.Commands
{
    public record UpdateProfileCommand(string UserId, string UserName) : IRequest<Unit>, IModeratedRequest
    {
        string IModeratedRequest.ModerationActorId => UserId;
        IEnumerable<ModeratedText> IModeratedRequest.GetModeratedTexts() => new[] { new ModeratedText(ModerationTarget.UserName, UserName, "смена ника") };
    }
}
