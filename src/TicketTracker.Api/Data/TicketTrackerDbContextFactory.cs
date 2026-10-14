using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TicketTracker.Api.Data;

public sealed class TicketTrackerDbContextFactory : IDesignTimeDbContextFactory<TicketTrackerDbContext>
{
    public TicketTrackerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TicketTrackerDbContext>()
            .UseSqlite("Data Source=tickettracker.db")
            .Options;

        return new TicketTrackerDbContext(options);
    }
}
