using Moq;
using NUnit.Framework;
using SubscriptionTracker.Api.Application.DTOs;
using SubscriptionTracker.Api.Application.Exceptions;
using SubscriptionTracker.Api.Application.Interfaces;
using SubscriptionTracker.Api.Application.Services;
using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Tests;

[TestFixture]
public class AuthServiceTests
{
    private sealed class Fixture
    {
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<IJwtTokenGenerator> Jwt { get; } = new();

        public Fixture()
        {
            Hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
            Hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            Jwt.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("jwt-token-123");
            Users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        public AuthService CreateSut() => new(Users.Object, Hasher.Object, Jwt.Object);
    }

    [Test]
    public void RegisterAsync_NullRequest_ThrowsArgumentNullException()
    {
        var sut = new Fixture().CreateSut();
        Assert.ThrowsAsync<ArgumentNullException>(() => sut.RegisterAsync(null!));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-an-email")]
    [TestCase("user@nodot")]
    [TestCase("user@")]
    [TestCase("Display Name <user@example.com>")]
    public void RegisterAsync_InvalidEmail_ThrowsValidationException(string? email)
    {
        var sut = new Fixture().CreateSut();

        Assert.ThrowsAsync<ValidationException>(() =>
            sut.RegisterAsync(new RegisterRequest(email!, "secret123")));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("12345")]
    public void RegisterAsync_NullOrShortPassword_ThrowsValidationException(string? password)
    {
        var sut = new Fixture().CreateSut();

        Assert.ThrowsAsync<ValidationException>(() =>
            sut.RegisterAsync(new RegisterRequest("user@example.com", password!)));
    }

    [Test]
    public void RegisterAsync_TooLongPassword_ThrowsValidationException()
    {
        var sut = new Fixture().CreateSut();
        var longPassword = new string('a', 101);

        Assert.ThrowsAsync<ValidationException>(() =>
            sut.RegisterAsync(new RegisterRequest("user@example.com", longPassword)));
    }

    [Test]
    public void RegisterAsync_DuplicateEmail_ThrowsConflictExceptionWithoutSaving()
    {
        var f = new Fixture();
        f.Users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new User { Email = "user@example.com" });

        Assert.ThrowsAsync<ConflictException>(() =>
            f.CreateSut().RegisterAsync(new RegisterRequest("  USER@example.com ", "secret123")));

        f.Users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task RegisterAsync_ValidRequest_NormalizesEmailHashesPasswordSavesUserAndReturnsToken()
    {
        var f = new Fixture();
        f.Users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
               .ReturnsAsync((User?)null);

        var response = await f.CreateSut().RegisterAsync(new RegisterRequest("  User@Example.COM ", "secret123"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Email, Is.EqualTo("user@example.com"));
            Assert.That(response.AccessToken, Is.EqualTo("jwt-token-123"));
            Assert.That(response.UserId, Is.Not.EqualTo(Guid.Empty));
        });
        f.Hasher.Verify(h => h.Hash("secret123"), Times.Once());
        f.Users.Verify(u => u.AddAsync(
            It.Is<User>(usr => usr.Email == "user@example.com" && usr.PasswordHash == "hashed-password"),
            It.IsAny<CancellationToken>()), Times.Once());
        f.Jwt.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Once());
    }

    [Test]
    public void LoginAsync_NullRequest_ThrowsArgumentNullException()
    {
        var sut = new Fixture().CreateSut();
        Assert.ThrowsAsync<ArgumentNullException>(() => sut.LoginAsync(null!));
    }

    [Test]
    public void LoginAsync_EmptyPassword_ThrowsValidationException()
    {
        var sut = new Fixture().CreateSut();

        Assert.ThrowsAsync<ValidationException>(() =>
            sut.LoginAsync(new LoginRequest("user@example.com", "   ")));
    }

    [Test]
    public void LoginAsync_UserNotFound_ThrowsUnauthorizedException()
    {
        var f = new Fixture();
        f.Users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
               .ReturnsAsync((User?)null);

        Assert.ThrowsAsync<UnauthorizedException>(() =>
            f.CreateSut().LoginAsync(new LoginRequest("user@example.com", "secret123")));
    }

    [Test]
    public void LoginAsync_WrongPassword_ThrowsUnauthorizedException()
    {
        var f = new Fixture();
        f.Users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new User { Email = "user@example.com", PasswordHash = "stored-hash" });
        f.Hasher.Setup(h => h.Verify("wrong-pass", "stored-hash")).Returns(false);

        Assert.ThrowsAsync<UnauthorizedException>(() =>
            f.CreateSut().LoginAsync(new LoginRequest("user@example.com", "wrong-pass")));

        f.Jwt.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never());
    }

    [Test]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
    {
        var f = new Fixture();
        var user = new User { Email = "user@example.com", PasswordHash = "stored-hash" };
        f.Users.Setup(u => u.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
               .ReturnsAsync(user);
        f.Hasher.Setup(h => h.Verify("secret123", "stored-hash")).Returns(true);

        var response = await f.CreateSut().LoginAsync(new LoginRequest("USER@example.com", "secret123"));

        Assert.Multiple(() =>
        {
            Assert.That(response.UserId, Is.EqualTo(user.Id));
            Assert.That(response.Email, Is.EqualTo("user@example.com"));
            Assert.That(response.AccessToken, Is.EqualTo("jwt-token-123"));
        });
    }
}
