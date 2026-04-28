namespace NotificationService.Models;

public class Notification
{
    public int Id { get; set; }
    public int PassengerId { get; set; }
    public int? RelatedBookingId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
