using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;

namespace NotificationService.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly NotificationDbContext _db;

    public NotificationController(NotificationDbContext db)
    {
        _db = db;
    }

    [HttpGet("my")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> GetMyNotifications()
    {
        var passengerId = GetPassengerId();
        if (passengerId == 0)
            return Unauthorized(new { message = "Invalid token." });

        return Ok(await GetNotificationsForPassenger(passengerId));
    }

    [HttpGet("my/unread-count")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> GetMyUnreadCount()
    {
        var passengerId = GetPassengerId();
        if (passengerId == 0)
            return Unauthorized(new { message = "Invalid token." });

        var count = await _db.Notifications
            .CountAsync(n => n.PassengerId == passengerId && !n.IsRead);

        return Ok(new { unreadCount = count });
    }

    [HttpGet("passenger/{passengerId}")]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> GetByPassenger(int passengerId)
    {
        return Ok(await GetNotificationsForPassenger(passengerId));
    }

    [HttpGet("passenger/{passengerId}/unread-count")]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> GetUnreadCount(int passengerId)
    {
        var count = await _db.Notifications
            .CountAsync(n => n.PassengerId == passengerId && !n.IsRead);

        return Ok(new { unreadCount = count });
    }

    [HttpPut("{id}/read")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var passengerId = GetPassengerId();
        if (passengerId == 0)
            return Unauthorized(new { message = "Invalid token." });

        var notification = await _db.Notifications.FindAsync(id);
        if (notification == null)
            return NotFound(new { message = "Notification not found." });

        if (notification.PassengerId != passengerId)
            return Forbid();

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        return Ok(notification);
    }

    private async Task<List<Models.Notification>> GetNotificationsForPassenger(int passengerId)
    {
        return await _db.Notifications
            .Where(n => n.PassengerId == passengerId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    private int GetPassengerId()
    {
        var sub = User?.FindFirst("sub")?.Value;
        return int.TryParse(sub, out var passengerId) ? passengerId : 0;
    }
}
