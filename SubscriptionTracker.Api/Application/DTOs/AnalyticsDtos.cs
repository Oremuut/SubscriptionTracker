using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Api.Application.DTOs;

public record ExpenseItemDto(
    Guid SubscriptionId,
    string Name,
    decimal UnitPrice,
    BillingPeriod BillingPeriod,
    int ChargesCount,
    decimal TotalAmount);

public record ExpenseSummaryDto(
    DateOnly From,
    DateOnly To,
    decimal TotalAmount,
    int TotalChargesCount,
    IReadOnlyList<ExpenseItemDto> Items);
