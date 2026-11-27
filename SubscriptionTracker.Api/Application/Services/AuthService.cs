using System.Net.Mail;
using SubscriptionTracker.Api.Application.DTOs;
using SubscriptionTracker.Api.Application.Exceptions;
using SubscriptionTracker.Api.Application.Interfaces;
using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Api.Application.Services;

/// <summary>
/// Сервис регистрации и аутентификации пользователей.
/// </summary>
public class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtGenerator)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormalizeAndValidateEmail(request.Email);
        ValidatePassword(request.Password);

        var existing = await users.GetByEmailAsync(email, ct);
        if (existing is not null)
            throw new ConflictException("User with this email already exists.");

        var user = new User
        {
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            CreatedAtUtc = DateTime.UtcNow
        };

        await users.AddAsync(user, ct);

        var token = jwtGenerator.GenerateToken(user);
        return new AuthResponse(user.Id, user.Email, token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormalizeAndValidateEmail(request.Email);
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Password is required.");

        var user = await users.GetByEmailAsync(email, ct);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        var token = jwtGenerator.GenerateToken(user);
        return new AuthResponse(user.Id, user.Email, token);
    }

    private static string NormalizeAndValidateEmail(string? rawEmail)
    {
        if (string.IsNullOrWhiteSpace(rawEmail))
            throw new ValidationException("Email is required.");

        var normalized = rawEmail.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(normalized, out var parsed) || parsed.Address != normalized || !normalized.Contains('.'))
            throw new ValidationException("Invalid email format.");

        return normalized;
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new ValidationException("Password must be at least 6 characters long.");

        if (password.Length > 100)
            throw new ValidationException("Password cannot exceed 100 characters.");
    }
}
