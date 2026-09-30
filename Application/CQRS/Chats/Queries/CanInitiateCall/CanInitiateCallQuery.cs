using System;
using MediatR;

namespace Application.CQRS.Chats.Queries
{
    public record CanInitiateCallQuery(Guid ChatId, string CallerId, string TargetUserId) : IRequest<bool>;
}
