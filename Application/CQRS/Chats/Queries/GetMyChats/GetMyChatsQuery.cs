using System.Collections.Generic;
using MediatR;
using Application.CQRS.Chats.DTOs;

namespace Application.CQRS.Chats.Queries
{
    public record GetMyChatsQuery(string UserId) : IRequest<IReadOnlyList<ChatDto>>;
}
