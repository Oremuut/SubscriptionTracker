using SubscriptionTracker.Api.Application.DTOs;
using SubscriptionTracker.Api.Application.Exceptions;
using SubscriptionTracker.Api.Application.Interfaces;
using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Api.Application.Services;

/// <summary>
/// Сервис управления подписками пользователя и контроля доступа к ним.
/// </summary>
public class SubscriptionService(ISubscriptionRepository subscriptions)
{
    public async Task<IReadOnlyList<SubscriptionDto>> GetAllAsync(
        Guid userId, SubscriptionStatus? status = null, CancellationToken ct = default)
    {
        var items = await subscriptions.GetByUserIdAsync(userId, status, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<SubscriptionDto> GetByIdAsync(Guid userId, Guid subscriptionId, CancellationToken ct = default)
    {
        var entity = await GetOwnedSubscriptionAsync(userId, subscriptionId, ct);
        return ToDto(entity);
    }

    public async Task<SubscriptionDto> CreateAsync(
        Guid userId, CreateSubscriptionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateFields(request.Name, request.Price, request.BillingPeriod, request.Status, request.StartDate, request.EndDate, request.PaymentNote);

        var entity = new Subscription
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Price = decimal.Round(request.Price, 2, MidpointRounding.AwayFromZero),
            BillingPeriod = request.BillingPeriod,
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            PaymentNote = string.IsNullOrWhiteSpace(request.PaymentNote) ? null : request.PaymentNote.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await subscriptions.AddAsync(entity, ct);
        return ToDto(entity);
    }

    public async Task<SubscriptionDto> UpdateAsync(
        Guid userId, Guid subscriptionId, UpdateSubscriptionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await GetOwnedSubscriptionAsync(userId, subscriptionId, ct);

        ValidateFields(request.Name, request.Price, request.BillingPeriod, request.Status, request.StartDate, request.EndDate, request.PaymentNote);

        entity.Name = request.Name.Trim();
        entity.Price = decimal.Round(request.Price, 2, MidpointRounding.AwayFromZero);
        entity.BillingPeriod = request.BillingPeriod;
        entity.Status = request.Status;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.PaymentNote = string.IsNullOrWhiteSpace(request.PaymentNote) ? null : request.PaymentNote.Trim();

        await subscriptions.UpdateAsync(entity, ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid userId, Guid subscriptionId, CancellationToken ct = default)
    {
        var entity = await GetOwnedSubscriptionAsync(userId, subscriptionId, ct);
        await subscriptions.DeleteAsync(entity, ct);
    }

    private async Task<Subscription> GetOwnedSubscriptionAsync(Guid userId, Guid subscriptionId, CancellationToken ct)
    {
        var entity = await subscriptions.GetByIdAsync(subscriptionId, ct)
            ?? throw new NotFoundException("Subscription not found.");

        if (entity.UserId != userId)
            throw new ForbiddenException("Access to this subscription is forbidden.");

        return entity;
    }

    private static void ValidateFields(
        string? name,
        decimal price,
        BillingPeriod period,
        SubscriptionStatus status,
        DateOnly startDate,
        DateOnly? endDate,
        string? paymentNote)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Subscription name is required.");

        if (name.Trim().Length > 100)
            throw new ValidationException("Subscription name cannot exceed 100 characters.");

        if (price < 0)
            throw new ValidationException("Price cannot be negative.");

        if (!Enum.IsDefined(period))
            throw new ValidationException("Unsupported billing period.");

        if (!Enum.IsDefined(status))
            throw new ValidationException("Unsupported subscription status.");

        if (endDate.HasValue && endDate.Value < startDate)
            throw new ValidationException("End date cannot be earlier than start date.");

        if (paymentNote is not null && paymentNote.Trim().Length > 200)
            throw new ValidationException("Payment note cannot exceed 200 characters.");
    }

    private static SubscriptionDto ToDto(Subscription s) => new(
        s.Id,
        s.Name,
        s.Price,
        s.BillingPeriod,
        s.Status,
        s.StartDate,
        s.EndDate,
        s.PaymentNote,
        s.CreatedAtUtc);
}
