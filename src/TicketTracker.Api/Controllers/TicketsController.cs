using Microsoft.AspNetCore.Mvc;
using TicketTracker.Api.Contracts;
using TicketTracker.Api.Services;

namespace TicketTracker.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:int}/tickets")]
[Produces("application/json")]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TicketResponse>>> List(
        int projectId,
        [FromQuery] TicketListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await tickets.ListAsync(projectId, query, cancellationToken));
    }

    [HttpGet("{ticketId:int}")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Get(int projectId, int ticketId, CancellationToken cancellationToken)
    {
        return Ok(await tickets.GetAsync(projectId, ticketId, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Create(
        int projectId,
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await tickets.CreateAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId, ticketId = ticket.Id }, ticket);
    }

    [HttpPut("{ticketId:int}")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Update(
        int projectId,
        int ticketId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await tickets.UpdateAsync(projectId, ticketId, request, cancellationToken));
    }

    [HttpDelete("{ticketId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int projectId, int ticketId, CancellationToken cancellationToken)
    {
        await tickets.DeleteAsync(projectId, ticketId, cancellationToken);
        return NoContent();
    }
}
