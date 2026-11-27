namespace SubscriptionTracker.Api.Application.DTOs;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(Guid UserId, string Email, string AccessToken);
