using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Api.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}

public interface ISubscriptionRepository
{
    Task<IReadOnlyList<Subscription>> GetByUserIdAsync(Guid userId, SubscriptionStatus? status, CancellationToken ct);
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Subscription subscription, CancellationToken ct);
    Task UpdateAsync(Subscription subscription, CancellationToken ct);
    Task DeleteAsync(Subscription subscription, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
