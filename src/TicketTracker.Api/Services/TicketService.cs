using Microsoft.EntityFrameworkCore;
using TicketTracker.Api.Contracts;
using TicketTracker.Api.Data;
using TicketTracker.Api.Domain;
using TicketTracker.Api.Errors;

namespace TicketTracker.Api.Services;

public interface ITicketService
{
    Task<PagedResult<TicketResponse>> ListAsync(int projectId, TicketListQuery query, CancellationToken cancellationToken);

    Task<TicketResponse> GetAsync(int projectId, int ticketId, CancellationToken cancellationToken);

    Task<TicketResponse> CreateAsync(int projectId, CreateTicketRequest request, CancellationToken cancellationToken);

    Task<TicketResponse> UpdateAsync(int projectId, int ticketId, UpdateTicketRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int projectId, int ticketId, CancellationToken cancellationToken);
}

public sealed class TicketService(TicketTrackerDbContext db) : ITicketService
{
    public async Task<PagedResult<TicketResponse>> ListAsync(
        int projectId,
        TicketListQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureProjectExistsAsync(projectId, cancellationToken);

        var tickets = db.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Project)
            .Include(ticket => ticket.Assignee)
            .Include(ticket => ticket.Reporter)
            .Where(ticket => ticket.ProjectId == projectId);

        if (query.Status is TicketStatus status)
        {
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (query.AssigneeId is int assigneeId)
        {
            tickets = tickets.Where(ticket => ticket.AssigneeId == assigneeId);
        }

        var totalCount = await tickets.CountAsync(cancellationToken);
        var items = await tickets
            .OrderBy(ticket => ticket.Number)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketResponse>(
            items.Select(Mappers.ToResponse).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<TicketResponse> GetAsync(int projectId, int ticketId, CancellationToken cancellationToken)
    {
        var ticket = await LoadTicketAsync(projectId, ticketId, tracking: false, cancellationToken);
        return Mappers.ToResponse(ticket);
    }

    public async Task<TicketResponse> CreateAsync(
        int projectId,
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var title = RequireTitle(request.Title);
        var description = NormalizeDescription(request.Description);
        if (request.Type is null || !Enum.IsDefined(request.Type.Value))
        {
            throw new BadRequestException("Type is required.");
        }

        if (request.Priority is null || !Enum.IsDefined(request.Priority.Value))
        {
            throw new BadRequestException("Priority is required.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            throw new BadRequestException("Status is not valid.");
        }

        var reporter = await RequireUserAsync(request.ReporterId, "Reporter", cancellationToken);
        var assignee = await ResolveAssigneeAsync(request.AssigneeId, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var project = await db.Projects.FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            throw new NotFoundException($"Project {projectId} was not found.");
        }

        var number = project.NextTicketNumber;
        project.NextTicketNumber = number + 1;
        var now = DateTimeOffset.UtcNow;
        var ticket = new Ticket
        {
            Project = project,
            ProjectId = project.Id,
            Number = number,
            Key = TicketKeys.Format(project.Key, number),
            Title = title,
            Description = description,
            Type = request.Type.Value,
            Priority = request.Priority.Value,
            Status = request.Status,
            Assignee = assignee,
            AssigneeId = assignee?.Id,
            Reporter = reporter,
            ReporterId = reporter.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Mappers.ToResponse(ticket);
    }

    public async Task<TicketResponse> UpdateAsync(
        int projectId,
        int ticketId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var title = RequireTitle(request.Title);
        var description = NormalizeDescription(request.Description);
        if (request.Type is null || !Enum.IsDefined(request.Type.Value))
        {
            throw new BadRequestException("Type is required.");
        }

        if (request.Priority is null || !Enum.IsDefined(request.Priority.Value))
        {
            throw new BadRequestException("Priority is required.");
        }

        if (request.Status is null || !Enum.IsDefined(request.Status.Value))
        {
            throw new BadRequestException("Status is required.");
        }

        var assignee = await ResolveAssigneeAsync(request.AssigneeId, cancellationToken);
        var ticket = await LoadTicketAsync(projectId, ticketId, tracking: true, cancellationToken);
        ticket.Title = title;
        ticket.Description = description;
        ticket.Type = request.Type.Value;
        ticket.Priority = request.Priority.Value;
        ticket.Status = request.Status.Value;
        ticket.AssigneeId = assignee?.Id;
        ticket.Assignee = assignee;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Mappers.ToResponse(ticket);
    }

    public async Task DeleteAsync(int projectId, int ticketId, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(
            item => item.ProjectId == projectId && item.Id == ticketId,
            cancellationToken);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {ticketId} was not found in project {projectId}.");
        }

        db.Tickets.Remove(ticket);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureProjectExistsAsync(int projectId, CancellationToken cancellationToken)
    {
        var exists = await db.Projects.AnyAsync(project => project.Id == projectId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException($"Project {projectId} was not found.");
        }
    }

    private async Task<Ticket> LoadTicketAsync(
        int projectId,
        int ticketId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = db.Tickets
            .Include(ticket => ticket.Project)
            .Include(ticket => ticket.Assignee)
            .Include(ticket => ticket.Reporter)
            .Where(ticket => ticket.ProjectId == projectId && ticket.Id == ticketId);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ticket = await query.FirstOrDefaultAsync(cancellationToken);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket {ticketId} was not found in project {projectId}.");
        }

        return ticket;
    }

    private async Task<User> RequireUserAsync(int? userId, string role, CancellationToken cancellationToken)
    {
        if (userId is null)
        {
            throw new BadRequestException($"{role} is required.");
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new BadRequestException($"{role} {userId} was not found.");
        }

        return user;
    }

    private async Task<User?> ResolveAssigneeAsync(int? assigneeId, CancellationToken cancellationToken)
    {
        if (assigneeId is null)
        {
            return null;
        }

        return await RequireUserAsync(assigneeId, "Assignee", cancellationToken);
    }

    private static string RequireTitle(string title)
    {
        var trimmed = title.Trim();
        if (trimmed.Length == 0 || trimmed.Length > FieldLimits.TicketTitleMax)
        {
            throw new BadRequestException($"Title is required and must be at most {FieldLimits.TicketTitleMax} characters.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var normalized = Text.NormalizeOptional(description);
        if (normalized is not null && normalized.Length > FieldLimits.TicketDescriptionMax)
        {
            throw new BadRequestException($"Description must be at most {FieldLimits.TicketDescriptionMax} characters.");
        }

        return normalized;
    }
}
