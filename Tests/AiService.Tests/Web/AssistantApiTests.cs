using System.Net;
using System.Net.Http.Json;
using AiService.Application.Abstractions;
using AiService.Web.Controllers;

namespace AiService.Tests.Web;

public class AssistantApiTests
{
    [Test]
    public async Task PostChat_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/assistant/chat", new ChatRequest(null, "Freier Termin für Farbe"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    // Requirement "RBAC-konforme Sichtbarkeit von Terminvorschlägen": serverseitig erzwungen, auch
    // ueber den echten HTTP-Pfad (Claim -> Controller -> Wolverine-Handler), nicht nur in der
    // Application-Schicht direkt getestet.
    [Test]
    public async Task PostChat_AsEmployeeAskingForAnotherEmployee_ReturnsRestrictedReplyWithoutSuggestions()
    {
        using var factory = new AiServiceWebFactory();
        var employeeId = Guid.NewGuid();
        factory.EmployeeClient.Seed(new EmployeeSummaryDto(employeeId, "Anna", "Beispiel", true, []));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", employeeId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/assistant/chat",
            new ChatRequest(null, "Wann hat Mitarbeiter B nächste Woche Zeit für einen Haarschnitt?"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();

        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Suggestions).IsEmpty();
        await Assert.That(body.Reply).Contains("eigenen Kalender");
    }

    [Test]
    public async Task GetHistory_ForAnotherUsersSession_ReturnsNotFound()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "admin-1");

        var response = await client.GetAsync($"/api/assistant/sessions/{Guid.NewGuid()}/history");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
