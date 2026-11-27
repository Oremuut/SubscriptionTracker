using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionTracker.Api.Application.DTOs;
using SubscriptionTracker.Api.Application.Services;
using SubscriptionTracker.Api.Domain;

namespace SubscriptionTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SubscriptionsController(SubscriptionService subscriptionService) : ControllerBase
{
    /// <summary>
    /// Получить список подписок текущего пользователя.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionDto>>> GetAll(
        [FromQuery] SubscriptionStatus? status, CancellationToken ct)
    {
        var result = await subscriptionService.GetAllAsync(User.GetUserId(), status, ct);
        return Ok(result);
    }

    /// <summary>
    /// Получить подписку текущего пользователя по идентификатору.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await subscriptionService.GetByIdAsync(User.GetUserId(), id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Добавить новую подписку.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubscriptionDto>> Create(
        [FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        var created = await subscriptionService.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Обновить данные существующей подписки.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDto>> Update(
        Guid id, [FromBody] UpdateSubscriptionRequest request, CancellationToken ct)
    {
        var updated = await subscriptionService.UpdateAsync(User.GetUserId(), id, request, ct);
        return Ok(updated);
    }

    /// <summary>
    /// Удалить подписку.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await subscriptionService.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
