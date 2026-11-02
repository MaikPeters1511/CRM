using EmployeeService.Application.Notifications;
using EmployeeService.Domain.Notifications;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Application;

public class NotificationsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task GetNotifications_ForRecipientWithUnreadNotification_ReturnsItInUnreadList()
    {
        var repository = new FakeNotificationRepository();
        var recipientId = Guid.NewGuid();
        repository.Seed(Notification.Create(Guid.NewGuid(), recipientId, "Abwesenheit genehmigt.", "/link", Now));

        var result = await GetNotificationsHandler.Handle(new GetNotificationsQuery(recipientId), repository, CancellationToken.None);

        await Assert.That(result.Notifications).Count().IsEqualTo(1);
        await Assert.That(result.Notifications[0].IsRead).IsFalse();
    }

    [Test]
    public async Task GetNotifications_ReturnsNewestFirst()
    {
        var repository = new FakeNotificationRepository();
        var recipientId = Guid.NewGuid();
        var older = Notification.Create(Guid.NewGuid(), recipientId, "Alt", null, Now);
        var newer = Notification.Create(Guid.NewGuid(), recipientId, "Neu", null, Now.AddHours(1));
        repository.Seed(older);
        repository.Seed(newer);

        var result = await GetNotificationsHandler.Handle(new GetNotificationsQuery(recipientId), repository, CancellationToken.None);

        await Assert.That(result.Notifications[0].Id).IsEqualTo(newer.Id);
    }

    [Test]
    public async Task MarkAsRead_OnOwnUnreadNotification_SetsIsReadTrue()
    {
        var repository = new FakeNotificationRepository();
        var recipientId = Guid.NewGuid();
        var notification = Notification.Create(Guid.NewGuid(), recipientId, "Text", null, Now);
        repository.Seed(notification);

        var result = await MarkNotificationAsReadHandler.Handle(
            new MarkNotificationAsReadCommand(notification.Id, recipientId, IsAdmin: false), repository, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(MarkNotificationAsReadStatus.Success);
        await Assert.That(notification.IsRead).IsTrue();
    }

    [Test]
    public async Task MarkAsRead_OnForeignNotificationWithoutAdminRole_ReturnsForbiddenAndDoesNotChangeState()
    {
        var repository = new FakeNotificationRepository();
        var recipientId = Guid.NewGuid();
        var notification = Notification.Create(Guid.NewGuid(), recipientId, "Text", null, Now);
        repository.Seed(notification);

        var result = await MarkNotificationAsReadHandler.Handle(
            new MarkNotificationAsReadCommand(notification.Id, Guid.NewGuid(), IsAdmin: false), repository, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(MarkNotificationAsReadStatus.Forbidden);
        await Assert.That(notification.IsRead).IsFalse();
    }

    [Test]
    public async Task MarkAsRead_WithUnknownNotificationId_ReturnsNotFound()
    {
        var repository = new FakeNotificationRepository();

        var result = await MarkNotificationAsReadHandler.Handle(
            new MarkNotificationAsReadCommand(Guid.NewGuid(), Guid.NewGuid(), IsAdmin: true), repository, CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(MarkNotificationAsReadStatus.NotFound);
    }
}
