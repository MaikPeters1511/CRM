using IdentityService.Domain.Security;

namespace IdentityService.Tests.Domain;

/// <summary>Validierungsregeln der org-weiten Sicherheitsrichtlinie (identity-device-trust.openspec.md).</summary>
public class SecurityPolicyTests
{
    [Test]
    public async Task Update_WithValidValues_AppliesThem()
    {
        var policy = new SecurityPolicy();

        policy.Update(requireMfa: true, passwordMinLength: 12, passwordExpiryDays: 30, maxFailedLoginAttempts: 3, lockoutDurationMinutes: 30);

        await Assert.That(policy.RequireMfa).IsTrue();
        await Assert.That(policy.PasswordMinLength).IsEqualTo(12);
        await Assert.That(policy.PasswordExpiryDays).IsEqualTo(30);
        await Assert.That(policy.MaxFailedLoginAttempts).IsEqualTo(3);
        await Assert.That(policy.LockoutDurationMinutes).IsEqualTo(30);
    }

    [Test]
    public async Task Update_WithPasswordMinLengthBelowOwaspMinimum_Throws()
    {
        var policy = new SecurityPolicy();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Task.Run(() => policy.Update(false, passwordMinLength: 4, 90, 5, 15)));
    }

    [Test]
    public async Task Update_WithZeroMaxFailedLoginAttempts_Throws()
    {
        var policy = new SecurityPolicy();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Task.Run(() => policy.Update(false, 8, 90, maxFailedLoginAttempts: 0, 15)));
    }
}
