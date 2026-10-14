using System.ComponentModel.DataAnnotations;
using TicketTracker.Api.Domain;
using TicketTracker.Api.Validation;

namespace TicketTracker.Api.Contracts;

public sealed class CreateProjectRequest
{
    [ProjectKey]
    public string Key { get; set; } = "";

    [RequiredTrimmed(FieldLimits.ProjectNameMax)]
    public string Name { get; set; } = "";

    [MaxLength(FieldLimits.ProjectDescriptionMax)]
    public string? Description { get; set; }
}

public sealed class UpdateProjectRequest
{
    [RequiredTrimmed(FieldLimits.ProjectNameMax)]
    public string Name { get; set; } = "";

    [MaxLength(FieldLimits.ProjectDescriptionMax)]
    public string? Description { get; set; }
}

public sealed class CreateTicketRequest
{
    [RequiredTrimmed(FieldLimits.TicketTitleMax)]
    public string Title { get; set; } = "";

    [MaxLength(FieldLimits.TicketDescriptionMax)]
    public string? Description { get; set; }

    [Required]
    public TicketType? Type { get; set; }

    [Required]
    public TicketPriority? Priority { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.ToDo;

    [Range(1, int.MaxValue)]
    public int? AssigneeId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? ReporterId { get; set; }
}

public sealed class UpdateTicketRequest
{
    [RequiredTrimmed(FieldLimits.TicketTitleMax)]
    public string Title { get; set; } = "";

    [MaxLength(FieldLimits.TicketDescriptionMax)]
    public string? Description { get; set; }

    [Required]
    public TicketType? Type { get; set; }

    [Required]
    public TicketPriority? Priority { get; set; }

    [Required]
    public TicketStatus? Status { get; set; }

    [Range(1, int.MaxValue)]
    public int? AssigneeId { get; set; }
}

public sealed class TicketListQuery
{
    public TicketStatus? Status { get; set; }

    [Range(1, int.MaxValue)]
    public int? AssigneeId { get; set; }

    [Range(1, FieldLimits.PageMax)]
    public int Page { get; set; } = 1;

    [Range(1, FieldLimits.PageSizeMax)]
    public int PageSize { get; set; } = 20;
}
