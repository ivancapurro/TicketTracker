using TicketTracker.Api.Domain;

namespace TicketTracker.Api.Contracts;

public sealed record UserResponse(int Id, string Name, string Email);

public sealed record ProjectResponse(
    int Id,
    string Key,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record TicketResponse(
    int Id,
    string Key,
    int ProjectId,
    string ProjectKey,
    int Number,
    string Title,
    string? Description,
    TicketType Type,
    TicketPriority Priority,
    TicketStatus Status,
    UserResponse? Assignee,
    UserResponse Reporter,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
