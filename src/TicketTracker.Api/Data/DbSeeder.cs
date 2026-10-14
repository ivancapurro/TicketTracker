using Microsoft.EntityFrameworkCore;
using TicketTracker.Api.Domain;

namespace TicketTracker.Api.Data;

public static class DbSeeder
{
    public const string AdaEmail = "ada@example.com";
    public const string GraceEmail = "grace@example.com";
    public const string AlanEmail = "alan@example.com";

    public static async Task SeedAsync(
        TicketTrackerDbContext db,
        bool includeSampleData,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AnyAsync(cancellationToken))
        {
            db.Users.AddRange(
                new User { Name = "Ada Lovelace", Email = AdaEmail },
                new User { Name = "Grace Hopper", Email = GraceEmail },
                new User { Name = "Alan Turing", Email = AlanEmail });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!includeSampleData || await db.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

        var ada = await db.Users.SingleAsync(user => user.Email == AdaEmail, cancellationToken);
        var grace = await db.Users.SingleAsync(user => user.Email == GraceEmail, cancellationToken);
        var alan = await db.Users.SingleAsync(user => user.Email == AlanEmail, cancellationToken);

        var trackerCreated = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var websiteCreated = new DateTimeOffset(2026, 1, 10, 9, 30, 0, TimeSpan.Zero);

        var tracker = new Project
        {
            Key = "TT",
            Name = "Ticket Tracker",
            Description = "Internal engineering tracker.",
            NextTicketNumber = 3,
            CreatedAt = trackerCreated,
        };
        var website = new Project
        {
            Key = "WEB",
            Name = "Website",
            Description = "Public marketing site.",
            NextTicketNumber = 2,
            CreatedAt = websiteCreated,
        };

        db.Projects.AddRange(tracker, website);
        await db.SaveChangesAsync(cancellationToken);

        db.Tickets.AddRange(
            new Ticket
            {
                ProjectId = tracker.Id,
                Number = 1,
                Key = "TT-1",
                Title = "Login button does nothing on submit",
                Description = "Clicking Sign in leaves the form in place and no request is sent.",
                Type = TicketType.Bug,
                Priority = TicketPriority.High,
                Status = TicketStatus.InProgress,
                AssigneeId = ada.Id,
                ReporterId = grace.Id,
                CreatedAt = trackerCreated,
                UpdatedAt = trackerCreated.AddHours(2),
            },
            new Ticket
            {
                ProjectId = tracker.Id,
                Number = 2,
                Key = "TT-2",
                Title = "Add project settings page",
                Description = "Let a project admin edit the name and description.",
                Type = TicketType.Story,
                Priority = TicketPriority.Medium,
                Status = TicketStatus.ToDo,
                AssigneeId = null,
                ReporterId = alan.Id,
                CreatedAt = trackerCreated.AddMinutes(30),
                UpdatedAt = trackerCreated.AddMinutes(30),
            },
            new Ticket
            {
                ProjectId = website.Id,
                Number = 1,
                Key = "WEB-1",
                Title = "Publish launch announcement",
                Description = "Draft and publish the public launch post.",
                Type = TicketType.Task,
                Priority = TicketPriority.Low,
                Status = TicketStatus.Done,
                AssigneeId = grace.Id,
                ReporterId = ada.Id,
                CreatedAt = websiteCreated,
                UpdatedAt = websiteCreated.AddDays(3),
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}
