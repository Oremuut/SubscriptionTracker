using SubscriptionTracker.Api.Application.DTOs;
using SubscriptionTracker.Api.Application.Exceptions;
using SubscriptionTracker.Api.Application.Interfaces;
using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Api.Application.Services;

/// <summary>
/// Сервис расчёта расходов пользователя по хранимым подпискам за заданный период.
/// </summary>
public class AnalyticsService(ISubscriptionRepository subscriptions)
{
    public async Task<ExpenseSummaryDto> CalculateExpensesAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (from > to)
            throw new ValidationException("'from' date cannot be later than 'to' date.");

        var allSubscriptions = await subscriptions.GetByUserIdAsync(userId, null, ct);

        var items = new List<ExpenseItemDto>();
        decimal totalAmount = 0m;
        int totalChargesCount = 0;

        foreach (var sub in allSubscriptions)
        {
            if (sub.Status != SubscriptionStatus.Active)
                continue;

            int chargesCount = CountChargesInPeriod(sub, from, to);
            if (chargesCount == 0)
                continue;

            decimal itemTotal = decimal.Round(sub.Price * chargesCount, 2, MidpointRounding.AwayFromZero);
            totalAmount += itemTotal;
            totalChargesCount += chargesCount;

            items.Add(new ExpenseItemDto(
                sub.Id,
                sub.Name,
                sub.Price,
                sub.BillingPeriod,
                chargesCount,
                itemTotal));
        }

        return new ExpenseSummaryDto(from, to, totalAmount, totalChargesCount, items);
    }

    private static int CountChargesInPeriod(Subscription sub, DateOnly from, DateOnly to)
    {
        DateOnly effectiveEnd = sub.EndDate.HasValue && sub.EndDate.Value < to
            ? sub.EndDate.Value
            : to;

        if (sub.StartDate > effectiveEnd)
            return 0;

        int count = 0;
        for (int step = 0; ; step++)
        {
            DateOnly chargeDate = GetChargeDate(sub.StartDate, sub.BillingPeriod, step);
            if (chargeDate > effectiveEnd)
                break;

            if (chargeDate >= from)
                count++;
        }

        return count;
    }

    private static DateOnly GetChargeDate(DateOnly startDate, BillingPeriod period, int step) => period switch
    {
        BillingPeriod.Weekly => startDate.AddDays(step * 7),
        BillingPeriod.Monthly => startDate.AddMonths(step),
        BillingPeriod.Yearly => startDate.AddYears(step),
        _ => throw new ValidationException("Unsupported billing period.")
    };
}
