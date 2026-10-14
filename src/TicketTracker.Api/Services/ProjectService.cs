using Microsoft.EntityFrameworkCore;
using TicketTracker.Api.Contracts;
using TicketTracker.Api.Data;
using TicketTracker.Api.Domain;
using TicketTracker.Api.Errors;

namespace TicketTracker.Api.Services;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken cancellationToken);

    Task<ProjectResponse> GetAsync(int id, CancellationToken cancellationToken);

    Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);

    Task<ProjectResponse> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);
}

public sealed class ProjectService(TicketTrackerDbContext db) : IProjectService
{
    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .OrderBy(project => project.Key)
            .ToListAsync(cancellationToken);

        return projects.Select(Mappers.ToResponse).ToList();
    }

    public async Task<ProjectResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null)
        {
            throw new NotFoundException($"Project {id} was not found.");
        }

        return Mappers.ToResponse(project);
    }

    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var key = ProjectKeys.Normalize(request.Key);
        if (!ProjectKeys.IsValid(key))
        {
            throw new BadRequestException("Key must be 2 to 10 uppercase letters (A-Z), for example TT.");
        }

        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > FieldLimits.ProjectNameMax)
        {
            throw new BadRequestException($"Name is required and must be at most {FieldLimits.ProjectNameMax} characters.");
        }

        var description = Text.NormalizeOptional(request.Description);
        if (description is not null && description.Length > FieldLimits.ProjectDescriptionMax)
        {
            throw new BadRequestException($"Description must be at most {FieldLimits.ProjectDescriptionMax} characters.");
        }

        if (await db.Projects.AnyAsync(project => project.Key == key, cancellationToken))
        {
            throw new ConflictException($"A project with key '{key}' already exists.");
        }

        var project = new Project
        {
            Key = key,
            Name = name,
            Description = description,
            NextTicketNumber = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Projects.Add(project);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException($"A project with key '{key}' already exists.");
        }

        return Mappers.ToResponse(project);
    }

    public async Task<ProjectResponse> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await db.Projects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null)
        {
            throw new NotFoundException($"Project {id} was not found.");
        }

        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > FieldLimits.ProjectNameMax)
        {
            throw new BadRequestException($"Name is required and must be at most {FieldLimits.ProjectNameMax} characters.");
        }

        var description = Text.NormalizeOptional(request.Description);
        if (description is not null && description.Length > FieldLimits.ProjectDescriptionMax)
        {
            throw new BadRequestException($"Description must be at most {FieldLimits.ProjectDescriptionMax} characters.");
        }

        project.Name = name;
        project.Description = description;
        await db.SaveChangesAsync(cancellationToken);
        return Mappers.ToResponse(project);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var project = await db.Projects.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (project is null)
        {
            throw new NotFoundException($"Project {id} was not found.");
        }

        var hasTickets = await db.Tickets.AnyAsync(ticket => ticket.ProjectId == id, cancellationToken);
        if (hasTickets)
        {
            throw new ConflictException($"Project {id} cannot be deleted because it still has tickets.");
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }
}
