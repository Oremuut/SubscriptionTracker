using Moq;
using NUnit.Framework;
using SubscriptionTracker.Api.Application.Exceptions;
using SubscriptionTracker.Api.Application.Interfaces;
using SubscriptionTracker.Api.Application.Services;
using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Tests;

[TestFixture]
public class AnalyticsServiceTests
{
    private Mock<ISubscriptionRepository> _repo = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _repo = new Mock<ISubscriptionRepository>();
        _userId = Guid.NewGuid();
    }

    private AnalyticsService CreateSut() => new(_repo.Object);

    [Test]
    public void CalculateExpensesAsync_FromAfterTo_ThrowsValidationException()
    {
        var from = new DateOnly(2026, 6, 10);
        var to = new DateOnly(2026, 6, 1);

        Assert.ThrowsAsync<ValidationException>(() => CreateSut().CalculateExpensesAsync(_userId, from, to));
    }

    [Test]
    public async Task CalculateExpensesAsync_NoSubscriptions_ReturnsZeroTotals()
    {
        _repo.Setup(r => r.GetByUserIdAsync(_userId, null, It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<Subscription>());

        var result = await CreateSut().CalculateExpensesAsync(_userId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalAmount, Is.EqualTo(0m));
            Assert.That(result.TotalChargesCount, Is.EqualTo(0));
            Assert.That(result.Items, Is.Empty);
        });
    }

    [Test]
    public async Task CalculateExpensesAsync_IgnoresPausedCancelledAndOutOfWindowSubscriptions()
    {
        var subs = new[]
        {
            new Subscription
            {
                UserId = _userId,
                Name = "Paused",
                Price = 500m,
                BillingPeriod = BillingPeriod.Monthly,
                Status = SubscriptionStatus.Paused,
                StartDate = new DateOnly(2026, 1, 1)
            },
            new Subscription
            {
                UserId = _userId,
                Name = "Cancelled",
                Price = 700m,
                BillingPeriod = BillingPeriod.Monthly,
                Status = SubscriptionStatus.Cancelled,
                StartDate = new DateOnly(2026, 1, 1)
            },
            new Subscription
            {
                UserId = _userId,
                Name = "Future",
                Price = 300m,
                BillingPeriod = BillingPeriod.Monthly,
                Status = SubscriptionStatus.Active,
                StartDate = new DateOnly(2026, 4, 1)
            },
            new Subscription
            {
                UserId = _userId,
                Name = "AlreadyEnded",
                Price = 400m,
                BillingPeriod = BillingPeriod.Monthly,
                Status = SubscriptionStatus.Active,
                StartDate = new DateOnly(2025, 1, 1),
                EndDate = new DateOnly(2025, 12, 31)
            }
        };

        _repo.Setup(r => r.GetByUserIdAsync(_userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(subs);

        var result = await CreateSut().CalculateExpensesAsync(_userId, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalAmount, Is.EqualTo(0m));
            Assert.That(result.TotalChargesCount, Is.EqualTo(0));
            Assert.That(result.Items, Is.Empty);
        });
    }

    [Test]
    public async Task CalculateExpensesAsync_WeeklyMonthlyYearlyActiveSubscriptions_CalculatesExactCharges()
    {
        var subs = new[]
        {
            new Subscription
            {
                UserId = _userId,
                Name = "Weekly Gym",
                Price = 100m,
                BillingPeriod = BillingPeriod.Weekly,
                Status = SubscriptionStatus.Active,
                StartDate = new DateOnly(2025, 12, 25)
            },
            new Subscription
            {
                UserId = _userId,
                Name = "Netflix",
                Price = 999m,
                BillingPeriod = BillingPeriod.Monthly,
                Status = SubscriptionStatus.Active,
                StartDate = new DateOnly(2026, 1, 31)
            },
            new Subscription
            {
                UserId = _userId,
                Name = "Domain",
                Price = 1500m,
                BillingPeriod = BillingPeriod.Yearly,
                Status = SubscriptionStatus.Active,
                StartDate = new DateOnly(2025, 1, 15)
            }
        };

        _repo.Setup(r => r.GetByUserIdAsync(_userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(subs);

        var result = await CreateSut().CalculateExpensesAsync(_userId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalChargesCount, Is.EqualTo(7));
            Assert.That(result.TotalAmount, Is.EqualTo(5 * 100m + 999m + 1500m));
            Assert.That(result.Items, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task CalculateExpensesAsync_EndDateCutsOffRemainingChargesInclusive()
    {
        var sub = new Subscription
        {
            UserId = _userId,
            Name = "Яндекс Плюс",
            Price = 400m,
            BillingPeriod = BillingPeriod.Monthly,
            Status = SubscriptionStatus.Active,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 3, 10)
        };
        _repo.Setup(r => r.GetByUserIdAsync(_userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { sub });

        var result = await CreateSut().CalculateExpensesAsync(_userId, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalChargesCount, Is.EqualTo(3));
            Assert.That(result.TotalAmount, Is.EqualTo(1200m));
        });
    }

    [Test]
    public async Task CalculateExpensesAsync_SingleDayPeriodMatchingChargeDate_CountsOneCharge()
    {
        var day = new DateOnly(2026, 5, 15);
        var sub = new Subscription
        {
            UserId = _userId,
            Name = "Premier",
            Price = 299m,
            BillingPeriod = BillingPeriod.Monthly,
            Status = SubscriptionStatus.Active,
            StartDate = new DateOnly(2026, 3, 15)
        };
        _repo.Setup(r => r.GetByUserIdAsync(_userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { sub });

        var result = await CreateSut().CalculateExpensesAsync(_userId, day, day);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalChargesCount, Is.EqualTo(1));
            Assert.That(result.TotalAmount, Is.EqualTo(299m));
        });
    }

    [Test]
    public void CalculateExpensesAsync_UnsupportedBillingPeriod_ThrowsValidationException()
    {
        var sub = new Subscription
        {
            UserId = _userId,
            Name = "Broken",
            Price = 100m,
            BillingPeriod = (BillingPeriod)999,
            Status = SubscriptionStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        };
        _repo.Setup(r => r.GetByUserIdAsync(_userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { sub });

        Assert.ThrowsAsync<ValidationException>(() =>
            CreateSut().CalculateExpensesAsync(_userId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
    }
}
