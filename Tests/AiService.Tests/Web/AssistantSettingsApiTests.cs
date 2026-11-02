using System.Net;
using System.Net.Http.Json;
using AiService.Application.Settings;
using AiService.Web.Controllers;

namespace AiService.Tests.Web;

public class AssistantSettingsApiTests
{
    [Test]
    public async Task GetSettings_AsAdmin_ReturnsCurrentConfiguration()
    {
        using var factory = new AiServiceWebFactory();
        factory.ApiKeyInspector.Configured = true;
        factory.ApiKeyInspector.Masked = "sk-••••1234";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "admin-1");

        var response = await client.GetAsync("/api/assistant/settings");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AssistantSettingsDto>();

        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Provider).IsEqualTo("OpenAi");
        await Assert.That(body.ApiKeyConfigured).IsTrue();
        await Assert.That(body.MaskedApiKey).IsEqualTo("sk-••••1234");
    }

    [Test]
    public async Task GetSettings_AsEmployee_ReturnsForbidden()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "employee-1");

        var response = await client.GetAsync("/api/assistant/settings");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetSettings_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/assistant/settings");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PutSettings_DisablingAssistant_TakesEffectImmediatelyOnChatEndpoint()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "admin-1");

        var putResponse = await client.PutAsJsonAsync(
            "/api/assistant/settings",
            new UpdateAssistantSettingsRequest(false, "OpenAi", "gpt-4o-mini", "http://qdrant:6333"));
        putResponse.EnsureSuccessStatusCode();
        var putBody = await putResponse.Content.ReadFromJsonAsync<AssistantSettingsDto>();

        await Assert.That(putBody!.Enabled).IsFalse();
        await Assert.That(putBody.RestartRequired).IsFalse();

        var chatResponse = await client.PostAsJsonAsync("/api/assistant/chat", new ChatRequest(null, "Freier Termin für Farbe"));

        await Assert.That(chatResponse.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task PutSettings_ChangingProvider_MarksRestartRequired()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "admin-1");

        var response = await client.PutAsJsonAsync(
            "/api/assistant/settings",
            new UpdateAssistantSettingsRequest(true, "LmStudio", "local-model", "http://qdrant:6333"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AssistantSettingsDto>();

        await Assert.That(body!.RestartRequired).IsTrue();
        await Assert.That(body.Provider).IsEqualTo("LmStudio");
    }

    [Test]
    public async Task PutSettings_AsEmployee_ReturnsForbiddenAndDoesNotChangeSettings()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "employee-1");

        var response = await client.PutAsJsonAsync(
            "/api/assistant/settings",
            new UpdateAssistantSettingsRequest(false, "OpenAi", "gpt-4o-mini", "http://qdrant:6333"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That((await factory.SettingsRepository.GetAsync(CancellationToken.None))!.Enabled).IsTrue();
    }

    [Test]
    public async Task SyncNow_AsAdmin_ReturnsPreviousSyncTimestampAndUpdatesGetAfterwards()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "admin-1");

        // docs/adr/0003: der taegliche Konsistenz-Sync laeuft bereits einmal direkt beim Start (Initial-Sync)
        // - der dabei gesetzte Zeitpunkt ist der erwartete "vorherige" Wert fuer den Sofort-Sync.
        var beforeSettings = await (await client.GetAsync("/api/assistant/settings")).Content.ReadFromJsonAsync<AssistantSettingsDto>();

        var response = await client.PostAsync("/api/assistant/settings/sync-now", null);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SyncNowResponse>();

        await Assert.That(body).IsNotNull();
        await Assert.That(body!.PreviousSyncAt).IsEqualTo(beforeSettings!.LastIndexSyncAt);

        var getResponse = await client.GetAsync("/api/assistant/settings");
        var settings = await getResponse.Content.ReadFromJsonAsync<AssistantSettingsDto>();
        await Assert.That(settings!.LastIndexSyncAt).IsEqualTo(body.SyncedAt);
    }

    [Test]
    public async Task SyncNow_AsEmployee_ReturnsForbidden()
    {
        using var factory = new AiServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "employee-1");

        var response = await client.PostAsync("/api/assistant/settings/sync-now", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
