using Moq;
using NUnit.Framework;
using SubscriptionTracker.Api.Application.DTOs;
using SubscriptionTracker.Api.Application.Exceptions;
using SubscriptionTracker.Api.Application.Interfaces;
using SubscriptionTracker.Api.Application.Services;
using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Tests;

[TestFixture]
public class SubscriptionServiceTests
{
    private sealed class Fixture
    {
        public Mock<ISubscriptionRepository> Repo { get; } = new();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid OtherUserId { get; } = Guid.NewGuid();

        public Fixture()
        {
            Repo.Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Repo.Setup(r => r.DeleteAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        public SubscriptionService CreateSut() => new(Repo.Object);
    }

    [Test]
    public async Task GetAllAsync_PassesUserIdAndStatusAndMapsToDtos()
    {
        var f = new Fixture();
        var sub = new Subscription
        {
            UserId = f.UserId,
            Name = "Яндекс Плюс",
            Price = 399m,
            BillingPeriod = BillingPeriod.Monthly,
            Status = SubscriptionStatus.Active,
            StartDate = new DateOnly(2026, 1, 10),
            PaymentNote = "Карта Т-Банк"
        };
        f.Repo.Setup(r => r.GetByUserIdAsync(f.UserId, SubscriptionStatus.Active, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { sub });

        var result = await f.CreateSut().GetAllAsync(f.UserId, SubscriptionStatus.Active);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Id, Is.EqualTo(sub.Id));
            Assert.That(result[0].Name, Is.EqualTo("Яндекс Плюс"));
            Assert.That(result[0].Price, Is.EqualTo(399m));
            Assert.That(result[0].PaymentNote, Is.EqualTo("Карта Т-Банк"));
        });
    }

    [Test]
    public void GetByIdAsync_NotFound_ThrowsNotFoundException()
    {
        var f = new Fixture();
        f.Repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Subscription?)null);

        Assert.ThrowsAsync<NotFoundException>(() => f.CreateSut().GetByIdAsync(f.UserId, Guid.NewGuid()));
    }

    [Test]
    public void GetByIdAsync_OwnedByAnotherUser_ThrowsForbiddenException()
    {
        var f = new Fixture();
        var sub = new Subscription { UserId = f.OtherUserId, Name = "Netflix" };
        f.Repo.Setup(r => r.GetByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        Assert.ThrowsAsync<ForbiddenException>(() => f.CreateSut().GetByIdAsync(f.UserId, sub.Id));
    }

    [Test]
    public async Task GetByIdAsync_OwnedByUser_ReturnsDto()
    {
        var f = new Fixture();
        var sub = new Subscription
        {
            UserId = f.UserId,
            Name = "Netflix",
            Price = 999m,
            BillingPeriod = BillingPeriod.Monthly,
            Status = SubscriptionStatus.Active,
            StartDate = new DateOnly(2026, 2, 1)
        };
        f.Repo.Setup(r => r.GetByIdAsync(sub.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        var dto = await f.CreateSut().GetByIdAsync(f.UserId, sub.Id);

        Assert.That(dto.Id, Is.EqualTo(sub.Id));
        Assert.That(dto.Name, Is.EqualTo("Netflix"));
    }

    [Test]
    public void CreateAsync_NullRequest_ThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(() => new Fixture().CreateSut().CreateAsync(Guid.NewGuid(), null!));
    }

    [TestCase("", 100, (int)BillingPeriod.Monthly, (int)SubscriptionStatus.Active, 0)]
    [TestCase("   ", 100, (int)BillingPeriod.Monthly, (int)SubscriptionStatus.Active, 0)]
    [TestCase("Netflix", -1, (int)BillingPeriod.Monthly, (int)SubscriptionStatus.Active, 0)]
    [TestCase("Netflix", 100, 99, (int)SubscriptionStatus.Active, 0)]
    [TestCase("Netflix", 100, (int)BillingPeriod.Monthly, 99, 0)]
    [TestCase("Netflix", 100, (int)BillingPeriod.Monthly, (int)SubscriptionStatus.Active, -1)]
    public void CreateAsync_InvalidFields_ThrowsValidationException(
        string name, decimal price, int period, int status, int endDateOffsetDays)
    {
        var f = new Fixture();
        var start = new DateOnly(2026, 5, 10);
        DateOnly? end = endDateOffsetDays < 0 ? start.AddDays(endDateOffsetDays) : null;
        var req = new CreateSubscriptionRequest(
            name, price, (BillingPeriod)period, (SubscriptionStatus)status, start, end, null);

        Assert.ThrowsAsync<ValidationException>(() => f.CreateSut().CreateAsync(f.UserId, req));
        f.Repo.Verify(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public void CreateAsync_NameOrNoteTooLong_ThrowsValidationException()
    {
        var f = new Fixture();
        var start = new DateOnly(2026, 5, 10);

        var tooLongNameReq = new CreateSubscriptionRequest(
            new string('x', 101), 100m, BillingPeriod.Monthly, SubscriptionStatus.Active, start, null, null);
        var tooLongNoteReq = new CreateSubscriptionRequest(
            "Valid", 100m, BillingPeriod.Monthly, SubscriptionStatus.Active, start, null, new string('y', 201));

        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<ValidationException>(() => f.CreateSut().CreateAsync(f.UserId, tooLongNameReq));
            Assert.ThrowsAsync<ValidationException>(() => f.CreateSut().CreateAsync(f.UserId, tooLongNoteReq));
        });
    }

    [Test]
    public async Task CreateAsync_ValidRequest_TrimsFieldsRoundsPriceAndPersists()
    {
        var f = new Fixture();
        var req = new CreateSubscriptionRequest(
            "  Premier  ",
            299.125m,
            BillingPeriod.Monthly,
            SubscriptionStatus.Active,
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 1),
            "   ");

        var dto = await f.CreateSut().CreateAsync(f.UserId, req);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Name, Is.EqualTo("Premier"));
            Assert.That(dto.Price, Is.EqualTo(299.13m));
            Assert.That(dto.PaymentNote, Is.Null);
        });
        f.Repo.Verify(r => r.AddAsync(
            It.Is<Subscription>(s => s.UserId == f.UserId && s.Name == "Premier" && s.Price == 299.13m),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task CreateAsync_WithPaymentNote_TrimsPaymentNote()
    {
        var f = new Fixture();
        var req = new CreateSubscriptionRequest(
            "Netflix", 500m, BillingPeriod.Monthly, SubscriptionStatus.Active,
            new DateOnly(2026, 3, 1), null, "  T-Bank  ");

        var dto = await f.CreateSut().CreateAsync(f.UserId, req);

        Assert.That(dto.PaymentNote, Is.EqualTo("T-Bank"));
    }

    [Test]
    public void UpdateAsync_NullRequest_ThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(() =>
            new Fixture().CreateSut().UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    [Test]
    public void UpdateAsync_OwnedByAnotherUser_ThrowsForbiddenException()
    {
        var f = new Fixture();
        var existing = new Subscription { UserId = f.OtherUserId, Name = "Old" };
        f.Repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var req = new UpdateSubscriptionRequest(
            "New", 10m, BillingPeriod.Weekly, SubscriptionStatus.Paused, new DateOnly(2026, 1, 1), null, null);

        Assert.ThrowsAsync<ForbiddenException>(() => f.CreateSut().UpdateAsync(f.UserId, existing.Id, req));
        f.Repo.Verify(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task UpdateAsync_ValidRequest_UpdatesFieldsAndPersists()
    {
        var f = new Fixture();
        var existing = new Subscription
        {
            UserId = f.UserId,
            Name = "Old",
            Price = 100m,
            BillingPeriod = BillingPeriod.Monthly,
            Status = SubscriptionStatus.Active,
            StartDate = new DateOnly(2026, 1, 1)
        };
        f.Repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var req = new UpdateSubscriptionRequest(
            "  Updated Service  ",
            1200m,
            BillingPeriod.Yearly,
            SubscriptionStatus.Paused,
            new DateOnly(2026, 2, 1),
            new DateOnly(2027, 2, 1),
            "  Сбер  ");

        var updated = await f.CreateSut().UpdateAsync(f.UserId, existing.Id, req);

        Assert.Multiple(() =>
        {
            Assert.That(updated.Name, Is.EqualTo("Updated Service"));
            Assert.That(updated.Price, Is.EqualTo(1200m));
            Assert.That(updated.BillingPeriod, Is.EqualTo(BillingPeriod.Yearly));
            Assert.That(updated.Status, Is.EqualTo(SubscriptionStatus.Paused));
            Assert.That(updated.EndDate, Is.EqualTo(new DateOnly(2027, 2, 1)));
            Assert.That(updated.PaymentNote, Is.EqualTo("Сбер"));
        });
        f.Repo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task UpdateAsync_WithWhitespaceNote_ClearsPaymentNote()
    {
        var f = new Fixture();
        var existing = new Subscription
        {
            UserId = f.UserId,
            Name = "Old",
            Price = 100m,
            BillingPeriod = BillingPeriod.Monthly,
            Status = SubscriptionStatus.Active,
            StartDate = new DateOnly(2026, 1, 1),
            PaymentNote = "Old card"
        };
        f.Repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var req = new UpdateSubscriptionRequest(
            "New", 150m, BillingPeriod.Monthly, SubscriptionStatus.Active,
            new DateOnly(2026, 1, 1), null, "   ");

        var updated = await f.CreateSut().UpdateAsync(f.UserId, existing.Id, req);

        Assert.That(updated.PaymentNote, Is.Null);
    }

    [Test]
    public void DeleteAsync_NotFound_ThrowsNotFoundException()
    {
        var f = new Fixture();
        f.Repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Subscription?)null);

        Assert.ThrowsAsync<NotFoundException>(() => f.CreateSut().DeleteAsync(f.UserId, Guid.NewGuid()));
    }

    [Test]
    public void DeleteAsync_OwnedByAnotherUser_ThrowsForbiddenException()
    {
        var f = new Fixture();
        var existing = new Subscription { UserId = f.OtherUserId };
        f.Repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        Assert.ThrowsAsync<ForbiddenException>(() => f.CreateSut().DeleteAsync(f.UserId, existing.Id));
        f.Repo.Verify(r => r.DeleteAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task DeleteAsync_OwnedByUser_DeletesEntity()
    {
        var f = new Fixture();
        var existing = new Subscription { UserId = f.UserId };
        f.Repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await f.CreateSut().DeleteAsync(f.UserId, existing.Id);

        f.Repo.Verify(r => r.DeleteAsync(existing, It.IsAny<CancellationToken>()), Times.Once());
    }
}
