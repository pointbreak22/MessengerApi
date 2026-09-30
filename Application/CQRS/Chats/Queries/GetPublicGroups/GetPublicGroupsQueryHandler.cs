using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Chats.DTOs;

namespace Application.CQRS.Chats.Queries
{
    public class GetPublicGroupsQueryHandler : IRequestHandler<GetPublicGroupsQuery, (IReadOnlyList<PublicGroupDto> Items, int Total)>
    {
        private readonly IChatRepository _chats;

        public GetPublicGroupsQueryHandler(IChatRepository chats)
        {
            _chats = chats;
        }

        public async Task<(IReadOnlyList<PublicGroupDto> Items, int Total)> Handle(GetPublicGroupsQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await _chats.GetPublicGroupsAsync(request.Page, request.PageSize, request.Search);
            return (items.Select(c => PublicGroupDto.FromEntity(c, request.UserId)).ToList(), total);
        }
    }
}
