namespace SubscriptionTracker.Api.Domain;

/// <summary>
/// Периодичность списания платы за подписку.
/// </summary>
public enum BillingPeriod
{
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}

/// <summary>
/// Текущий статус подписки.
/// </summary>
public enum SubscriptionStatus
{
    Active = 1,
    Paused = 2,
    Cancelled = 3
}
