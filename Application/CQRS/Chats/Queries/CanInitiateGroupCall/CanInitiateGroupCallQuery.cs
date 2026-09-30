using System;
using System.Collections.Generic;
using MediatR;

namespace Application.CQRS.Chats.Queries
{
    // Group counterpart of CanInitiateCallQuery — allows IsGroup chats (the
    // 1:1 query hard-rejects them) and checks the caller plus every invited
    // participant are actual members of the chat.
    public record CanInitiateGroupCallQuery(Guid ChatId, string CallerId, IReadOnlyList<string> ParticipantIds) : IRequest<bool>;
}
