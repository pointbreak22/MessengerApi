using System;
using MediatR;
using Application.CQRS.Chats.DTOs;

namespace Application.CQRS.Chats.Queries
{
    public record GetChatByIdQuery(Guid ChatId) : IRequest<ChatDto?>;
}
