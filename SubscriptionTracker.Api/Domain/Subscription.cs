namespace SubscriptionTracker.Api.Domain;

/// <summary>
/// Подписка пользователя на внешний сервис.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public BillingPeriod BillingPeriod { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? PaymentNote { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
