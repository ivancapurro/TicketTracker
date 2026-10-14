using TicketTracker.Api.Contracts;
using TicketTracker.Api.Domain;

namespace TicketTracker.Api.Services;

public static class Mappers
{
    public static UserResponse ToResponse(User user) => new(user.Id, user.Name, user.Email);

    public static ProjectResponse ToResponse(Project project) =>
        new(project.Id, project.Key, project.Name, project.Description, project.CreatedAt);

    public static TicketResponse ToResponse(Ticket ticket)
    {
        if (ticket.Project is null)
        {
            throw new InvalidOperationException("Ticket.Project must be loaded.");
        }

        if (ticket.Reporter is null)
        {
            throw new InvalidOperationException("Ticket.Reporter must be loaded.");
        }

        return new TicketResponse(
            ticket.Id,
            ticket.Key,
            ticket.ProjectId,
            ticket.Project.Key,
            ticket.Number,
            ticket.Title,
            ticket.Description,
            ticket.Type,
            ticket.Priority,
            ticket.Status,
            ticket.Assignee is null ? null : ToResponse(ticket.Assignee),
            ToResponse(ticket.Reporter),
            ticket.CreatedAt,
            ticket.UpdatedAt);
    }
}
