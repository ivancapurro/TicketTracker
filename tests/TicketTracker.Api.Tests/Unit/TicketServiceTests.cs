using TicketTracker.Api.Contracts;
using TicketTracker.Api.Domain;
using TicketTracker.Api.Errors;
using TicketTracker.Api.Services;
using TicketTracker.Api.Tests.Infrastructure;

namespace TicketTracker.Api.Tests.Unit;

public class TicketServiceTests
{
    [Fact]
    public async Task Create_assigns_incrementing_keys_that_are_not_reused()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (projects, tickets, ada) = Services(database);
        var project = await projects.CreateAsync(new CreateProjectRequest { Key = "TT", Name = "Ticket Tracker" }, CancellationToken.None);

        var first = await tickets.CreateAsync(project.Id, Ticket("Alpha", ada), CancellationToken.None);
        var second = await tickets.CreateAsync(project.Id, Ticket("Beta", ada, description: "   "), CancellationToken.None);

        Assert.Equal("TT-1", first.Key);
        Assert.Equal(1, first.Number);
        Assert.Equal(TicketStatus.ToDo, first.Status);
        Assert.Equal(first.CreatedAt, first.UpdatedAt);
        Assert.Null(second.Description);
        Assert.Equal("TT-2", second.Key);

        await tickets.DeleteAsync(project.Id, first.Id, CancellationToken.None);
        var third = await tickets.CreateAsync(project.Id, Ticket("Gamma", ada), CancellationToken.None);
        Assert.Equal("TT-3", third.Key);
    }

    [Fact]
    public async Task List_filters_by_status_and_assignee_and_pages_in_number_order()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (projects, tickets, ada) = Services(database);
        var grace = database.Context.Users.Single(user => user.Email == "grace@example.com").Id;
        var project = await projects.CreateAsync(new CreateProjectRequest { Key = "TT", Name = "Ticket Tracker" }, CancellationToken.None);

        var created = new List<TicketResponse>();
        for (var index = 1; index <= 5; index++)
        {
            created.Add(await tickets.CreateAsync(project.Id, Ticket($"Item {index}", ada, assigneeId: index % 2 == 0 ? grace : ada), CancellationToken.None));
        }

        await tickets.UpdateAsync(project.Id, created[0].Id, Update(created[0], TicketStatus.InProgress), CancellationToken.None);
        await tickets.UpdateAsync(project.Id, created[1].Id, new UpdateTicketRequest
        {
            Title = created[1].Title,
            Description = created[1].Description,
            Type = created[1].Type,
            Priority = created[1].Priority,
            Status = TicketStatus.InProgress,
            AssigneeId = null,
        }, CancellationToken.None);

        var inProgress = await tickets.ListAsync(project.Id, new TicketListQuery { Status = TicketStatus.InProgress }, CancellationToken.None);
        Assert.Equal(2, inProgress.TotalCount);
        Assert.Equal(["TT-1", "TT-2"], inProgress.Items.Select(ticket => ticket.Key).ToArray());
        Assert.Null(inProgress.Items.Single(ticket => ticket.Key == "TT-2").Assignee);

        var assignedToAda = await tickets.ListAsync(project.Id, new TicketListQuery { AssigneeId = ada }, CancellationToken.None);
        Assert.Equal(["TT-1", "TT-3", "TT-5"], assignedToAda.Items.Select(ticket => ticket.Key).ToArray());

        var page = await tickets.ListAsync(project.Id, new TicketListQuery { Page = 2, PageSize = 2 }, CancellationToken.None);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(["TT-3", "TT-4"], page.Items.Select(ticket => ticket.Key).ToArray());

        var unknownAssignee = await tickets.ListAsync(project.Id, new TicketListQuery { AssigneeId = 9999 }, CancellationToken.None);
        Assert.Empty(unknownAssignee.Items);
        Assert.Equal(0, unknownAssignee.TotalCount);
    }

    [Fact]
    public async Task Update_changes_fields_without_changing_the_key_and_missing_refs_fail()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (projects, tickets, ada) = Services(database);
        var project = await projects.CreateAsync(new CreateProjectRequest { Key = "TT", Name = "Ticket Tracker" }, CancellationToken.None);
        var created = await tickets.CreateAsync(project.Id, Ticket("Alpha", ada, assigneeId: ada), CancellationToken.None);

        var updated = await tickets.UpdateAsync(project.Id, created.Id, new UpdateTicketRequest
        {
            Title = "  Renamed  ",
            Description = " Details ",
            Type = TicketType.Bug,
            Priority = TicketPriority.High,
            Status = TicketStatus.InProgress,
            AssigneeId = null,
        }, CancellationToken.None);

        Assert.Equal("TT-1", updated.Key);
        Assert.Equal("Renamed", updated.Title);
        Assert.Equal("Details", updated.Description);
        Assert.Equal(TicketType.Bug, updated.Type);
        Assert.Equal(TicketPriority.High, updated.Priority);
        Assert.Equal(TicketStatus.InProgress, updated.Status);
        Assert.Null(updated.Assignee);
        Assert.Equal(ada, updated.Reporter.Id);
        Assert.True(updated.UpdatedAt >= updated.CreatedAt);

        var unknownReporter = await Assert.ThrowsAsync<BadRequestException>(() =>
            tickets.CreateAsync(project.Id, Ticket("Missing", reporterId: 9999), CancellationToken.None));
        Assert.Contains("Reporter", unknownReporter.Message, StringComparison.Ordinal);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            tickets.GetAsync(project.Id + 50, created.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            tickets.ListAsync(project.Id + 50, new TicketListQuery(), CancellationToken.None));
    }

    private static (ProjectService Projects, TicketService Tickets, int AdaId) Services(TestDatabase database)
    {
        var ada = database.Context.Users.Single(user => user.Email == "ada@example.com").Id;
        return (new ProjectService(database.Context), new TicketService(database.Context), ada);
    }

    private static CreateTicketRequest Ticket(string title, int reporterId, int? assigneeId = null, string? description = null) =>
        new()
        {
            Title = title,
            Description = description,
            Type = TicketType.Task,
            Priority = TicketPriority.Medium,
            ReporterId = reporterId,
            AssigneeId = assigneeId,
        };

    private static UpdateTicketRequest Update(TicketResponse ticket, TicketStatus status) =>
        new()
        {
            Title = ticket.Title,
            Description = ticket.Description,
            Type = ticket.Type,
            Priority = ticket.Priority,
            Status = status,
            AssigneeId = ticket.Assignee?.Id,
        };
}
