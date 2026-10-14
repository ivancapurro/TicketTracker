using Microsoft.EntityFrameworkCore;
using TicketTracker.Api.Domain;

namespace TicketTracker.Api.Data;

public sealed class TicketTrackerDbContext(DbContextOptions<TicketTrackerDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToStringConverter>()
            .HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(project => project.Key).HasMaxLength(FieldLimits.ProjectKeyMax).IsRequired();
            entity.HasIndex(project => project.Key).IsUnique();
            entity.Property(project => project.Name).HasMaxLength(FieldLimits.ProjectNameMax).IsRequired();
            entity.Property(project => project.Description).HasMaxLength(FieldLimits.ProjectDescriptionMax);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Name).HasMaxLength(FieldLimits.UserNameMax).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(FieldLimits.UserEmailMax).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(ticket => ticket.Key).HasMaxLength(32).IsRequired();
            entity.HasIndex(ticket => ticket.Key).IsUnique();
            entity.HasIndex(ticket => new { ticket.ProjectId, ticket.Number }).IsUnique();
            entity.HasIndex(ticket => ticket.Status);
            entity.HasIndex(ticket => ticket.AssigneeId);
            entity.Property(ticket => ticket.Title).HasMaxLength(FieldLimits.TicketTitleMax).IsRequired();
            entity.Property(ticket => ticket.Description).HasMaxLength(FieldLimits.TicketDescriptionMax);
            entity.Property(ticket => ticket.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(32);
            entity.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(32);

            entity.HasOne(ticket => ticket.Project)
                .WithMany(project => project.Tickets)
                .HasForeignKey(ticket => ticket.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ticket => ticket.Assignee)
                .WithMany()
                .HasForeignKey(ticket => ticket.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ticket => ticket.Reporter)
                .WithMany()
                .HasForeignKey(ticket => ticket.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
