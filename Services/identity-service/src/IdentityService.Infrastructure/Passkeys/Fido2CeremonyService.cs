using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using IdentityService.Application.Passkeys;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Passkeys;

/// <summary>
/// Gesamte WebAuthn-Server-Logik via Fido2NetLib (docs/adr/0004, Abschnitt 3) - keine
/// Eigenimplementierung von WebAuthn/CBOR/COSE. Challenge-State ist kurzlebig und wird NICHT in
/// identity_db persistiert, sondern in einem In-Memory-Cache mit TTL gehalten.
/// </summary>
public sealed class Fido2CeremonyService : IFido2CeremonyService
{
    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);

    private readonly IFido2 _fido2;
    private readonly IPasskeyCredentialRepository _credentialRepository;
    private readonly IMemoryCache _cache;

    public Fido2CeremonyService(
        IOptions<Fido2Options> options, IPasskeyCredentialRepository credentialRepository, IMemoryCache cache)
    {
        var config = options.Value;
        _fido2 = new Fido2(new Fido2Configuration
        {
            RPID = config.ServerDomain,
            RPName = config.ServerName,
            Origins = new HashSet<string>(config.Origins),
        });
        _credentialRepository = credentialRepository;
        _cache = cache;
    }

    public async Task<string> BeginRegistrationAsync(Guid userId, string email, string displayName, CancellationToken cancellationToken)
    {
        var existingCredentials = await _credentialRepository.ListByUserAsync(userId, cancellationToken);
        var excludeCredentials = existingCredentials
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var fido2User = new Fido2User
        {
            DisplayName = displayName,
            Name = email,
            Id = userId.ToByteArray(),
        };

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fido2User,
            ExcludeCredentials = excludeCredentials,
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None,
        });

        _cache.Set(RegistrationCacheKey(userId), options, ChallengeTtl);

        return options.ToJson();
    }

    public async Task<PasskeyRegistrationResult> CompleteRegistrationAsync(
        Guid userId, string attestationResponseJson, CancellationToken cancellationToken)
    {
        if (!_cache.TryGetValue(RegistrationCacheKey(userId), out CredentialCreateOptions? originalOptions) || originalOptions is null)
        {
            throw new InvalidOperationException("Keine offene Passkey-Registrierung gefunden (abgelaufen oder nie gestartet).");
        }

        _cache.Remove(RegistrationCacheKey(userId));

        var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationResponseJson)
            ?? throw new InvalidOperationException("Ungueltige Attestation-Response.");

        var result = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions = originalOptions,
            IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                await _credentialRepository.FindByCredentialIdAsync(args.CredentialId, ct) is null,
        }, cancellationToken);

        return new PasskeyRegistrationResult(result.Id, result.PublicKey, (uint)result.SignCount);
    }

    public Task<string> BeginAssertionAsync(CancellationToken cancellationToken)
    {
        // Usernameless/Discoverable-Credential-Flow: keine existingCredentials-Einschraenkung,
        // der Authenticator selbst waehlt den passenden Passkey aus (identity-passkeys-webauthn.openspec.md).
        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Preferred,
        });

        var token = Guid.NewGuid().ToString("N");
        _cache.Set(AssertionCacheKey(token), options, ChallengeTtl);

        return Task.FromResult(JsonSerializer.Serialize(new { token, options = JsonSerializer.Deserialize<JsonElement>(options.ToJson()) }));
    }

    public async Task<PasskeyAssertionResult> CompleteAssertionAsync(string assertionResponseJson, CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<AssertionEnvelope>(assertionResponseJson)
            ?? throw new InvalidOperationException("Ungueltige Assertion-Response.");

        if (!_cache.TryGetValue(AssertionCacheKey(envelope.Token), out AssertionOptions? originalOptions) || originalOptions is null)
        {
            throw new InvalidOperationException("Keine offene Passkey-Login-Challenge gefunden (abgelaufen oder nie gestartet).");
        }

        _cache.Remove(AssertionCacheKey(envelope.Token));

        var clientResponse = envelope.Response;
        var credential = await _credentialRepository.FindByCredentialIdAsync(clientResponse.RawId, cancellationToken)
            ?? throw new InvalidOperationException("Unbekanntes Passkey-Credential.");

        var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = clientResponse,
            OriginalOptions = originalOptions,
            StoredPublicKey = credential.PublicKey,
            StoredSignatureCounter = credential.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(true),
        }, cancellationToken);

        return new PasskeyAssertionResult(clientResponse.RawId, (uint)result.SignCount);
    }

    private static string RegistrationCacheKey(Guid userId) => $"webauthn-registration:{userId}";

    private static string AssertionCacheKey(string token) => $"webauthn-assertion:{token}";

    /// <summary>
    /// Buendelt Challenge-Token und die eigentliche AuthenticatorAssertionRawResponse in einem JSON-Objekt,
    /// da reine Discoverable-Credential-Assertions keinen anderen Korrelationswert zur Ceremony liefern.
    /// </summary>
    private sealed class AssertionEnvelope
    {
        public string Token { get; set; } = string.Empty;

        public AuthenticatorAssertionRawResponse Response { get; set; } = null!;
    }
}
