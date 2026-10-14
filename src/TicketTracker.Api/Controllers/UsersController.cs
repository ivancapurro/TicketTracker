using Microsoft.AspNetCore.Mvc;
using TicketTracker.Api.Contracts;
using TicketTracker.Api.Services;

namespace TicketTracker.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await users.ListAsync(cancellationToken));
    }
}
