using Microsoft.EntityFrameworkCore;
using SubscriptionTracker.Api.Application.Interfaces;
using SubscriptionTracker.Api.Domain;
using SubscriptionTracker.Api.Infrastructure.Persistence;

namespace SubscriptionTracker.Api.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await db.Users.AddAsync(user, ct);
        await db.SaveChangesAsync(ct);
    }
}
