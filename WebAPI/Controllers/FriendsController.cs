using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Application.CQRS.Friends.Commands;
using Application.CQRS.Friends.Queries;
using Application.CQRS.Users.Queries;

namespace WebAPI.Controllers
{
    public class FriendsController : ApiControllerBase
    {
        private readonly IMediator _mediator;

        public FriendsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("{friendId}")]
        public async Task<IActionResult> SendRequest(string friendId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (userId == friendId) return BadRequest("Cannot add yourself.");

            var friend = await _mediator.Send(new GetUserByIdQuery(friendId));
            if (friend == null) return NotFound();

            await _mediator.Send(new SendFriendRequestCommand(userId, friendId));
            return Ok();
        }

        [HttpPost("{friendId}/accept")]
        public async Task<IActionResult> AcceptRequest(string friendId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var req = await _mediator.Send(new FindFriendshipQuery(friendId, userId));
            if (req == null) return NotFound();
            await _mediator.Send(new AcceptFriendRequestCommand(req.UserId, userId));
            return Ok();
        }

        [HttpDelete("{friendId}")]
        public async Task<IActionResult> RemoveFriend(string friendId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var req = await _mediator.Send(new FindFriendshipQuery(userId, friendId));
            if (req == null) return NotFound();
            await _mediator.Send(new RemoveFriendCommand(userId, friendId));
            return NoContent();
        }

        [HttpGet("me")]
        public async Task<IActionResult> MyFriends()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var friends = await _mediator.Send(new GetFriendsQuery(userId));
            return Ok(friends);
        }

        [HttpGet("requests")]
        public async Task<IActionResult> PendingRequests()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var pending = await _mediator.Send(new GetPendingFriendRequestsQuery(userId));
            return Ok(pending);
        }
    }
}
