using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketTracker.Api.Data;

namespace TicketTracker.Api.Tests.Infrastructure;

public sealed class TicketTrackerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"tickettracker-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(FindApiContentRoot());
        builder.UseSetting("ConnectionStrings:TicketTracker", $"Data Source={_databasePath}");
        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<TicketTrackerDbContext>)
                    || descriptor.ServiceType == typeof(TicketTrackerDbContext)
                    || (descriptor.ServiceType.IsGenericType
                        && descriptor.ServiceType.GenericTypeArguments.Contains(typeof(TicketTrackerDbContext))))
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<TicketTrackerDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Delete(_databasePath);
        Delete(_databasePath + "-wal");
        Delete(_databasePath + "-shm");
    }

    private static string FindApiContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TicketTracker.Api");
            if (File.Exists(Path.Combine(candidate, "TicketTracker.Api.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate src/TicketTracker.Api.");
    }

    private static void Delete(string path)
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

[CollectionDefinition(ApiCollection.Name)]
public sealed class ApiCollection : ICollectionFixture<TicketTrackerApiFactory>
{
    public const string Name = "api";
}
