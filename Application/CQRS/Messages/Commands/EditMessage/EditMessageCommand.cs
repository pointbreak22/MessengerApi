using System;
using MediatR;

namespace Application.CQRS.Messages.Commands
{
    public record EditMessageCommand(Guid MessageId, string RequesterId, string Text) : IRequest<MediatR.Unit>;
}
