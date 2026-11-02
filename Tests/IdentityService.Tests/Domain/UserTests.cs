using IdentityService.Domain.Users;

namespace IdentityService.Tests.Domain;

/// <summary>Account-Lockout-Zustandsmaschine (identity-device-trust.openspec.md).</summary>
public class UserTests
{
    private static User NewUser() => new(Guid.NewGuid(), "user@crm.local", "hash", UserRole.Employee, "Test User");

    [Test]
    public async Task RecordFailedLoginAttempt_BelowThreshold_DoesNotLock()
    {
        var user = NewUser();
        var now = DateTimeOffset.UtcNow;

        user.RecordFailedLoginAttempt(maxAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15), nowUtc: now);

        await Assert.That(user.FailedLoginAttempts).IsEqualTo(1);
        await Assert.That(user.IsLockedOut(now)).IsFalse();
    }

    [Test]
    public async Task RecordFailedLoginAttempt_ReachingThreshold_Locks()
    {
        var user = NewUser();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLoginAttempt(maxAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15), nowUtc: now);
        }

        await Assert.That(user.IsLockedOut(now)).IsTrue();
        await Assert.That(user.IsLockedOut(now.AddMinutes(16))).IsFalse();
    }

    [Test]
    public async Task ResetFailedLoginAttempts_ClearsCounterAndLock()
    {
        var user = NewUser();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLoginAttempt(5, TimeSpan.FromMinutes(15), now);
        }

        user.ResetFailedLoginAttempts();

        await Assert.That(user.FailedLoginAttempts).IsEqualTo(0);
        await Assert.That(user.IsLockedOut(now)).IsFalse();
    }
}
