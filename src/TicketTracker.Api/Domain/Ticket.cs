namespace TicketTracker.Api.Domain;

public sealed class Ticket
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public int Number { get; set; }

    public string Key { get; set; } = "";

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public TicketType Type { get; set; }

    public TicketPriority Priority { get; set; }

    public TicketStatus Status { get; set; }

    public int? AssigneeId { get; set; }

    public User? Assignee { get; set; }

    public int ReporterId { get; set; }

    public User Reporter { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
