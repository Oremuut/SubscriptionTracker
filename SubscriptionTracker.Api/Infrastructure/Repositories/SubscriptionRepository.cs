using Microsoft.EntityFrameworkCore;
using SubscriptionTracker.Api.Application.Interfaces;
using SubscriptionTracker.Api.Domain;
using SubscriptionTracker.Api.Infrastructure.Persistence;

namespace SubscriptionTracker.Api.Infrastructure.Repositories;

public class SubscriptionRepository(AppDbContext db) : ISubscriptionRepository
{
    public async Task<IReadOnlyList<Subscription>> GetByUserIdAsync(
        Guid userId, SubscriptionStatus? status, CancellationToken ct)
    {
        var query = db.Subscriptions.AsNoTracking().Where(s => s.UserId == userId);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query.OrderBy(s => s.StartDate).ThenBy(s => s.Name).ToListAsync(ct);
    }

    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(Subscription subscription, CancellationToken ct)
    {
        await db.Subscriptions.AddAsync(subscription, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Subscription subscription, CancellationToken ct)
    {
        db.Subscriptions.Update(subscription);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Subscription subscription, CancellationToken ct)
    {
        db.Subscriptions.Remove(subscription);
        await db.SaveChangesAsync(ct);
    }
}
