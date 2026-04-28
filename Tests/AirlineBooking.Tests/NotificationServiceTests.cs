using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Controllers;
using NotificationService.Models;
using NUnit.Framework;

namespace AirlineBooking.Tests;

[TestFixture]
public class NotificationServiceTests
{
    [Test]
    public async Task GetUnreadCount_ReturnsOnlyUnreadNotificationsForPassenger()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.AddRange(
            new Notification { PassengerId = 1, Title = "Unread", Message = "Message", IsRead = false },
            new Notification { PassengerId = 1, Title = "Read", Message = "Message", IsRead = true },
            new Notification { PassengerId = 2, Title = "Other", Message = "Message", IsRead = false });
        await db.SaveChangesAsync();

        var controller = new NotificationController(db).WithUser(1, "STAFF");

        var response = await controller.GetUnreadCount(1);

        var ok = response as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Has.Property("unreadCount").EqualTo(1));
    }

    [Test]
    public async Task MarkAsRead_UpdatesNotificationReadState()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.Add(new Notification
        {
            Id = 1,
            PassengerId = 1,
            Title = "Booking confirmed",
            Message = "Your booking is confirmed.",
            IsRead = false
        });
        await db.SaveChangesAsync();

        var controller = new NotificationController(db).WithUser(1);

        var response = await controller.MarkAsRead(1);
        var notification = await db.Notifications.FindAsync(1);

        Assert.That(response, Is.TypeOf<OkObjectResult>());
        Assert.That(notification!.IsRead, Is.True);
    }

    [Test]
    public async Task GetByPassenger_ReturnsOnlyRequestedPassengerNotifications()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.AddRange(
            CreateNotification(passengerId: 1, title: "One"),
            CreateNotification(passengerId: 2, title: "Two"));
        await db.SaveChangesAsync();
        var controller = new NotificationController(db).WithUser(1, "STAFF");

        var response = await controller.GetByPassenger(1);

        var ok = response as OkObjectResult;
        var notifications = ok!.Value as List<Notification>;
        Assert.That(notifications, Has.Count.EqualTo(1));
        Assert.That(notifications!.Single().PassengerId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetByPassenger_ReturnsNewestNotificationsFirst()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.AddRange(
            CreateNotification(passengerId: 1, title: "Old", createdAt: DateTime.UtcNow.AddDays(-1)),
            CreateNotification(passengerId: 1, title: "New", createdAt: DateTime.UtcNow));
        await db.SaveChangesAsync();
        var controller = new NotificationController(db).WithUser(1, "STAFF");

        var response = await controller.GetByPassenger(1);
        var notifications = ((OkObjectResult)response).Value as List<Notification>;

        Assert.That(notifications!.First().Title, Is.EqualTo("New"));
    }

    [Test]
    public async Task GetByPassenger_ReturnsEmptyListWhenPassengerHasNoNotifications()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        var controller = new NotificationController(db).WithUser(1, "STAFF");

        var response = await controller.GetByPassenger(1);
        var notifications = ((OkObjectResult)response).Value as List<Notification>;

        Assert.That(notifications, Is.Empty);
    }

    [Test]
    public async Task GetUnreadCount_ReturnsZeroWhenAllNotificationsAreRead()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.Add(CreateNotification(passengerId: 1, isRead: true));
        await db.SaveChangesAsync();
        var controller = new NotificationController(db).WithUser(1, "STAFF");

        var response = await controller.GetUnreadCount(1);

        Assert.That(((OkObjectResult)response).Value, Has.Property("unreadCount").EqualTo(0));
    }

    [Test]
    public async Task MarkAsRead_ReturnsNotFoundForMissingNotification()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        var controller = new NotificationController(db).WithUser(1);

        var response = await controller.MarkAsRead(99);

        Assert.That(response, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task MarkAsRead_IsSafeForAlreadyReadNotification()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.Add(CreateNotification(passengerId: 1, isRead: true));
        await db.SaveChangesAsync();
        var controller = new NotificationController(db).WithUser(1);

        var response = await controller.MarkAsRead(1);

        Assert.That(response, Is.TypeOf<OkObjectResult>());
        Assert.That((await db.Notifications.FindAsync(1))!.IsRead, Is.True);
    }

    [Test]
    public async Task NotificationDbContext_PersistsRelatedBookingId()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.Add(CreateNotification(passengerId: 1, relatedBookingId: 42));
        await db.SaveChangesAsync();

        var notification = await db.Notifications.SingleAsync();

        Assert.That(notification.RelatedBookingId, Is.EqualTo(42));
    }

    [Test]
    public async Task NotificationDbContext_DefaultsNewNotificationToUnread()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.Add(new Notification
        {
            PassengerId = 1,
            Title = "Booking confirmed",
            Message = "Your booking is confirmed."
        });
        await db.SaveChangesAsync();

        var notification = await db.Notifications.SingleAsync();

        Assert.That(notification.IsRead, Is.False);
    }

    [Test]
    public async Task GetMyNotifications_ReturnsOnlyLoggedInPassengerNotifications()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.AddRange(
            CreateNotification(passengerId: 1, title: "Mine"),
            CreateNotification(passengerId: 2, title: "Other"));
        await db.SaveChangesAsync();
        var controller = new NotificationController(db).WithUser(1);

        var response = await controller.GetMyNotifications();
        var notifications = ((OkObjectResult)response).Value as List<Notification>;

        Assert.That(notifications, Has.Count.EqualTo(1));
        Assert.That(notifications!.Single().Title, Is.EqualTo("Mine"));
    }

    [Test]
    public async Task GetMyUnreadCount_ReturnsLoggedInPassengerUnreadCount()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.AddRange(
            CreateNotification(passengerId: 1, isRead: false),
            CreateNotification(passengerId: 1, isRead: true),
            CreateNotification(passengerId: 2, isRead: false));
        await db.SaveChangesAsync();
        var controller = new NotificationController(db).WithUser(1);

        var response = await controller.GetMyUnreadCount();

        Assert.That(((OkObjectResult)response).Value, Has.Property("unreadCount").EqualTo(1));
    }

    [Test]
    public async Task MarkAsRead_ForbidsNotificationOwnedByAnotherPassenger()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        db.Notifications.Add(CreateNotification(passengerId: 2));
        await db.SaveChangesAsync();
        var controller = new NotificationController(db).WithUser(1);

        var response = await controller.MarkAsRead(1);

        Assert.That(response, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task GetMyNotifications_ReturnsUnauthorizedWhenTokenHasNoPassengerId()
    {
        await using var db = TestHelpers.CreateNotificationDbContext();
        var controller = new NotificationController(db);

        var response = await controller.GetMyNotifications();

        Assert.That(response, Is.TypeOf<UnauthorizedObjectResult>());
    }

    private static Notification CreateNotification(
        int passengerId,
        string title = "Booking confirmed",
        bool isRead = false,
        DateTime? createdAt = null,
        int? relatedBookingId = null) => new()
    {
        PassengerId = passengerId,
        RelatedBookingId = relatedBookingId,
        Type = "BookingConfirmed",
        Title = title,
        Message = "Your booking is confirmed.",
        IsRead = isRead,
        CreatedAt = createdAt ?? DateTime.UtcNow
    };
}
