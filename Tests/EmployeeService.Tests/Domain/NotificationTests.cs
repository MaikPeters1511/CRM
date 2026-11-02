using EmployeeService.Domain.Notifications;

namespace EmployeeService.Tests.Domain;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Create_WithRecipientAndText_ReturnsUnreadNotification()
    {
        var id = Guid.NewGuid();
        var recipientEmployeeId = Guid.NewGuid();

        var notification = Notification.Create(id, recipientEmployeeId, "Dein Urlaubsantrag wurde genehmigt.", "/absences/123", Now);

        await Assert.That(notification.Id).IsEqualTo(id);
        await Assert.That(notification.RecipientEmployeeId).IsEqualTo(recipientEmployeeId);
        await Assert.That(notification.Text).IsEqualTo("Dein Urlaubsantrag wurde genehmigt.");
        await Assert.That(notification.Link).IsEqualTo("/absences/123");
        await Assert.That(notification.IsRead).IsFalse();
        await Assert.That(notification.CreatedAt).IsEqualTo(Now);
    }

    [Test]
    public async Task MarkAsRead_OnUnreadNotification_SetsIsReadTrue()
    {
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "Text", null, Now);

        notification.MarkAsRead();

        await Assert.That(notification.IsRead).IsTrue();
    }

    [Test]
    public async Task MarkAsRead_CalledTwice_IsIdempotent()
    {
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "Text", null, Now);

        notification.MarkAsRead();
        notification.MarkAsRead();

        await Assert.That(notification.IsRead).IsTrue();
    }
}
