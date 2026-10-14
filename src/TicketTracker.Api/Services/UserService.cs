using Microsoft.EntityFrameworkCore;
using TicketTracker.Api.Contracts;
using TicketTracker.Api.Data;

namespace TicketTracker.Api.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken);
}

public sealed class UserService(TicketTrackerDbContext db) : IUserService
{
    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(user => user.Name)
            .ToListAsync(cancellationToken);

        return users.Select(Mappers.ToResponse).ToList();
    }
}
