using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Api.Application.DTOs;

public record CreateSubscriptionRequest(
    string Name,
    decimal Price,
    BillingPeriod BillingPeriod,
    SubscriptionStatus Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? PaymentNote);

public record UpdateSubscriptionRequest(
    string Name,
    decimal Price,
    BillingPeriod BillingPeriod,
    SubscriptionStatus Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? PaymentNote);

public record SubscriptionDto(
    Guid Id,
    string Name,
    decimal Price,
    BillingPeriod BillingPeriod,
    SubscriptionStatus Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? PaymentNote,
    DateTime CreatedAtUtc);
