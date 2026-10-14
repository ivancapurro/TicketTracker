using Microsoft.EntityFrameworkCore;
using TicketTracker.Api.Data;

namespace TicketTracker.Api.Tests.Infrastructure;

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path;
    private readonly TicketTrackerDbContext _context;

    private TestDatabase(string path, TicketTrackerDbContext context)
    {
        _path = path;
        _context = context;
    }

    public TicketTrackerDbContext Context => _context;

    public static async Task<TestDatabase> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tickettracker-unit-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<TicketTrackerDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        var context = new TicketTrackerDbContext(options);
        await context.Database.MigrateAsync();
        await DbSeeder.SeedAsync(context, includeSampleData: false);
        return new TestDatabase(path, context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        TryDelete(_path);
        TryDelete(_path + "-wal");
        TryDelete(_path + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
