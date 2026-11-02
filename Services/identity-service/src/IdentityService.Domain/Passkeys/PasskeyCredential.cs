namespace IdentityService.Domain.Passkeys;

/// <summary>
/// WebAuthn/FIDO2-Credential eines Nutzers (identity-passkeys-webauthn.openspec.md).
/// Kryptografische Validierung (Attestation/Assertion) erfolgt ausschliesslich ueber Fido2NetLib
/// in der Infrastructure-Schicht - dieses Aggregat haelt nur die resultierenden, bereits
/// validierten Daten.
/// </summary>
public sealed class PasskeyCredential
{
    public PasskeyCredential(
        Guid id,
        Guid userId,
        byte[] credentialId,
        byte[] publicKey,
        uint signCount,
        string name)
    {
        Id = id;
        UserId = userId;
        CredentialId = credentialId;
        PublicKey = publicKey;
        SignCount = signCount;
        Name = name;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private PasskeyCredential()
    {
        CredentialId = [];
        PublicKey = [];
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public byte[] CredentialId { get; private set; }

    public byte[] PublicKey { get; private set; }

    public uint SignCount { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    /// <summary>
    /// Aktualisiert den Sign-Counter nach erfolgreicher Assertion. Ein neuer Counter kleiner-gleich
    /// dem gespeicherten Wert deutet auf einen geklonten Authenticator hin (WebAuthn Level 2,
    /// 7.2 Verifying Assertion, Schritt 17) und wird vom Aufrufer VOR diesem Aufruf geprueft.
    /// </summary>
    public void UpdateSignCount(uint newSignCount)
    {
        SignCount = newSignCount;
        LastUsedAtUtc = DateTimeOffset.UtcNow;
    }
}
