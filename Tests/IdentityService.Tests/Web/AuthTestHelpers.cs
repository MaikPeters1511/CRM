using System.Net.Http.Json;
using System.Text.Json;
using IdentityService.Domain.Devices;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Web.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Tests.Web;

/// <summary>
/// Seit identity-device-trust.openspec.md unterbricht ein Login von einem (fuer den jeweiligen
/// Nutzer) unbekannten Geraet den Flow mit <c>DeviceConfirmationRequired</c> statt direkt ein
/// Token auszustellen (docs/adr/0004, Abschnitt 5). Tests, die ein erfolgreiches Login erwarten,
/// bestaetigen das Testgeraet deshalb einmalig direkt in der DB (Umgehung des E-Mail-Codes, der
/// nur geloggt wird) und wiederholen den Login danach.
/// </summary>
internal static class AuthTestHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<HttpResponseMessage> LoginAsTrustedDeviceAsync(
        WebApplicationFactory<Program> factory, HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        // DeviceConfirmationRequired antwortet bewusst mit 200 OK (kein Fehler, sondern ein
        // Zwischenstatus) - die beiden Antwortformen werden daher am JSON-Body unterschieden, nicht
        // am Statuscode.
        var rawBody = await response.Content.ReadAsStringAsync();
        var challenge = JsonSerializer.Deserialize<LoginChallengeResponse>(rawBody, JsonOptions);

        if (challenge?.Status == "device_confirmation_required" && challenge.PendingDeviceConfirmationId is { } pendingId)
        {
            await TrustPendingDeviceAsync(factory, pendingId);
            response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        }

        return response;
    }

    private static async Task TrustPendingDeviceAsync(WebApplicationFactory<Program> factory, Guid pendingConfirmationId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var pending = await dbContext.PendingDeviceConfirmations.FirstAsync(p => p.Id == pendingConfirmationId);
        dbContext.KnownDevices.Add(new KnownDevice(Guid.NewGuid(), pending.UserId, pending.FingerprintHash, "test", "0.0.0.0/24"));
        await dbContext.SaveChangesAsync();
    }
}
