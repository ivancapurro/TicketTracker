namespace TicketTracker.Api.Domain;

public sealed class Project
{
    public int Id { get; set; }

    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    /// <summary>
    /// Next number to assign. Incremented on create and never decreased, so ticket keys are not reused.
    /// </summary>
    public int NextTicketNumber { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
