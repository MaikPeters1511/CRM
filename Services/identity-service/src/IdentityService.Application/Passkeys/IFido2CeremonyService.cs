namespace IdentityService.Application.Passkeys;

public sealed record PasskeyRegistrationResult(byte[] CredentialId, byte[] PublicKey, uint SignCount);

public sealed record PasskeyAssertionResult(byte[] CredentialId, uint NewSignCount);

/// <summary>
/// Kapselt die gesamte WebAuthn-Server-Logik hinter einem Application-Interface, implementiert in
/// Infrastructure mittels Fido2NetLib (docs/adr/0004, Abschnitt 3) - keine Eigenimplementierung von
/// WebAuthn/CBOR/COSE. Rohe JSON-Strings statt Fido2NetLib-Typen in der Signatur, damit die
/// Application-Schicht keine Abhaengigkeit auf Fido2NetLib benoetigt (Clean Architecture).
/// Challenge-State ist kurzlebig und wird NICHT in identity_db persistiert, sondern von der
/// Infrastructure-Implementierung in einem In-Memory-Cache mit TTL gehalten.
/// </summary>
public interface IFido2CeremonyService
{
    Task<string> BeginRegistrationAsync(Guid userId, string email, string displayName, CancellationToken cancellationToken);

    Task<PasskeyRegistrationResult> CompleteRegistrationAsync(
        Guid userId, string attestationResponseJson, CancellationToken cancellationToken);

    Task<string> BeginAssertionAsync(CancellationToken cancellationToken);

    Task<PasskeyAssertionResult> CompleteAssertionAsync(string assertionResponseJson, CancellationToken cancellationToken);
}
