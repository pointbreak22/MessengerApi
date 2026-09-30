using System;
using System.Collections.Generic;
using MediatR;
using Application.CQRS.Messages.DTOs;

namespace Application.CQRS.Messages.Queries
{
    public record GetMessagesQuery(Guid ChatId, int Limit, DateTime? Before) : IRequest<IReadOnlyList<MessageDto>>;
}
