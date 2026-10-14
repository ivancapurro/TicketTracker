using TicketTracker.Api.Contracts;
using TicketTracker.Api.Errors;
using TicketTracker.Api.Services;
using TicketTracker.Api.Tests.Infrastructure;

namespace TicketTracker.Api.Tests.Unit;

public class ProjectServiceTests
{
    [Fact]
    public async Task Create_trims_fields_and_lists_projects_by_key()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new ProjectService(database.Context);

        var website = await service.CreateAsync(new CreateProjectRequest
        {
            Key = " WEB ",
            Name = "  Website  ",
            Description = "  Public site  ",
        }, CancellationToken.None);
        var tracker = await service.CreateAsync(new CreateProjectRequest
        {
            Key = "TT",
            Name = "Ticket Tracker",
            Description = "   ",
        }, CancellationToken.None);

        Assert.Equal("WEB", website.Key);
        Assert.Equal("Website", website.Name);
        Assert.Equal("Public site", website.Description);
        Assert.Null(tracker.Description);
        Assert.Equal(TimeSpan.Zero, tracker.CreatedAt.Offset);

        var listed = await service.ListAsync(CancellationToken.None);
        Assert.Equal(["TT", "WEB"], listed.Select(project => project.Key).ToArray());
    }

    [Fact]
    public async Task Create_rejects_invalid_and_duplicate_keys()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new ProjectService(database.Context);

        var invalid = await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(new CreateProjectRequest
        {
            Key = "tt",
            Name = "Lowercase",
        }, CancellationToken.None));
        Assert.Contains("uppercase", invalid.Message, StringComparison.OrdinalIgnoreCase);

        await service.CreateAsync(new CreateProjectRequest { Key = "TT", Name = "Ticket Tracker" }, CancellationToken.None);
        var duplicate = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateProjectRequest
        {
            Key = "TT",
            Name = "Again",
        }, CancellationToken.None));
        Assert.Contains("already exists", duplicate.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_and_get_keep_the_key_and_missing_projects_are_not_found()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new ProjectService(database.Context);
        var created = await service.CreateAsync(new CreateProjectRequest { Key = "TT", Name = "Old" }, CancellationToken.None);

        var updated = await service.UpdateAsync(created.Id, new UpdateProjectRequest
        {
            Name = " New name ",
            Description = " Notes ",
        }, CancellationToken.None);

        Assert.Equal("TT", updated.Key);
        Assert.Equal("New name", updated.Name);
        Assert.Equal("Notes", updated.Description);
        Assert.Equal(updated.Name, (await service.GetAsync(created.Id, CancellationToken.None)).Name);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(created.Id + 100, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(created.Id + 100, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_is_rejected_while_tickets_exist_and_succeeds_after_they_are_gone()
    {
        await using var database = await TestDatabase.CreateAsync();
        var projects = new ProjectService(database.Context);
        var tickets = new TicketService(database.Context);
        var project = await projects.CreateAsync(new CreateProjectRequest { Key = "TT", Name = "Ticket Tracker" }, CancellationToken.None);
        var reporterId = database.Context.Users.Single(user => user.Email == "ada@example.com").Id;
        var ticket = await tickets.CreateAsync(project.Id, new CreateTicketRequest
        {
            Title = "First",
            Type = TicketTracker.Api.Domain.TicketType.Task,
            Priority = TicketTracker.Api.Domain.TicketPriority.Low,
            ReporterId = reporterId,
        }, CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => projects.DeleteAsync(project.Id, CancellationToken.None));
        Assert.Contains("still has tickets", conflict.Message, StringComparison.OrdinalIgnoreCase);

        await tickets.DeleteAsync(project.Id, ticket.Id, CancellationToken.None);
        await projects.DeleteAsync(project.Id, CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => projects.GetAsync(project.Id, CancellationToken.None));
    }
}
