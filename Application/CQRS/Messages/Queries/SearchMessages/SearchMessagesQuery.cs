using System.Collections.Generic;
using MediatR;
using Application.CQRS.Messages.DTOs;

namespace Application.CQRS.Messages.Queries
{
    public record SearchMessagesQuery(string UserId, string Search, int Limit) : IRequest<IReadOnlyList<MessageDto>>;
}
