using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionTracker.Api.Application.DTOs;
using SubscriptionTracker.Api.Application.Exceptions;
using SubscriptionTracker.Api.Application.Services;

namespace SubscriptionTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AnalyticsController(AnalyticsService analyticsService) : ControllerBase
{
    /// <summary>
    /// Рассчитать расходы пользователя по активным подпискам за период [from, to].
    /// </summary>
    [HttpGet("expenses")]
    [ProducesResponseType(typeof(ExpenseSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpenseSummaryDto>> GetExpenses(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        if (!from.HasValue || !to.HasValue)
            throw new ValidationException("Query parameters 'from' and 'to' are required (YYYY-MM-DD).");

        var summary = await analyticsService.CalculateExpensesAsync(User.GetUserId(), from.Value, to.Value, ct);
        return Ok(summary);
    }
}
