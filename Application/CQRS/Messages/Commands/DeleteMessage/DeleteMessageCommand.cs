using System;
using MediatR;

namespace Application.CQRS.Messages.Commands
{
    public record DeleteMessageCommand(Guid MessageId, string RequesterId) : IRequest<MediatR.Unit>;
}
