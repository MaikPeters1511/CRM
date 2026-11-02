using System.Net;
using EmployeeService.Domain.Notifications;

namespace EmployeeService.Tests.Web;

public class NotificationsApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task GetNotifications_AsOwner_ReturnsOwnUnreadNotification()
    {
        using var factory = new EmployeeServiceWebFactory();
        var recipientId = Guid.NewGuid();
        factory.NotificationRepository.Seed(Notification.Create(Guid.NewGuid(), recipientId, "Genehmigt.", "/link", Now));
        factory.NotificationRepository.Seed(Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "Fremd.", null, Now));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", recipientId.ToString());

        var response = await client.GetAsync("/api/notifications");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Genehmigt.");
        await Assert.That(body).DoesNotContain("Fremd.");
    }

    [Test]
    public async Task PostRead_OnOwnNotification_MarksAsReadAndReturnsNoContent()
    {
        using var factory = new EmployeeServiceWebFactory();
        var recipientId = Guid.NewGuid();
        var notification = Notification.Create(Guid.NewGuid(), recipientId, "Genehmigt.", null, Now);
        factory.NotificationRepository.Seed(notification);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", recipientId.ToString());

        var response = await client.PostAsync($"/api/notifications/{notification.Id}/read", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(notification.IsRead).IsTrue();
    }

    [Test]
    public async Task PostRead_OnForeignNotification_ReturnsForbidden()
    {
        using var factory = new EmployeeServiceWebFactory();
        var notification = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), "Genehmigt.", null, Now);
        factory.NotificationRepository.Seed(notification);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());

        var response = await client.PostAsync($"/api/notifications/{notification.Id}/read", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(notification.IsRead).IsFalse();
    }
}
