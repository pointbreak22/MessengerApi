using System;
using MediatR;

namespace Application.CQRS.Chats.Queries
{
    public record IsChatMemberQuery(Guid ChatId, string UserId) : IRequest<bool>;
}
