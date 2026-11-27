using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SubscriptionTracker.Api.Application.Exceptions;

namespace SubscriptionTracker.Api.Controllers;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(raw, out var userId))
            throw new UnauthorizedException("Invalid user token.");

        return userId;
    }
}
