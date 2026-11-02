using System.Net.Http.Json;
using System.Text.Json;

namespace AiService.Infrastructure.Clients;

public sealed class ServiceAccountOptions
{
    public string? Email { get; set; }

    public string? Password { get; set; }
}

public interface IServiceAccountTokenProvider
{
    /// <summary>Liefert ein gueltiges JWT des Service-Accounts oder <c>null</c>, wenn keiner konfiguriert/Login fehlgeschlagen ist.</summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Meldet ai-service als technischen Service-Account bei identity-service an (<c>POST /api/auth/login</c>)
/// und cached das Token bis kurz vor Ablauf. Dient ausschliesslich Hintergrundprozessen ohne
/// eingehenden Nutzer-Request (Index-Sync, docs/adr/0003). Bewusst KEIN selbst signiertes Token: ai-service
/// kennt den Signing-Key nur zur Validierung und kann so keine Tokens faelschen.
/// Schlaegt der Login fehl, wird <c>null</c> geliefert - der Folge-Call scheitert dann an der
/// [Authorize]-Pruefung des Zielservices statt die Anwendung zu stoppen.
/// </summary>
public sealed class ServiceAccountTokenProvider(
    HttpClient httpClient,
    ServiceAccountOptions options,
    TimeProvider timeProvider) : IServiceAccountTokenProvider
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && timeProvider.GetUtcNow() < _expiresAt - RefreshSkew)
            {
                return _token;
            }

            try
            {
                var response = await httpClient.PostAsJsonAsync(
                    "/api/auth/login", new { email = options.Email, password = options.Password }, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);
                if (body is null || string.IsNullOrWhiteSpace(body.AccessToken))
                {
                    return null;
                }

                _token = body.AccessToken;
                _expiresAt = body.ExpiresAtUtc;
                return _token;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
}
