using IdentityService.Domain.Audit;
using IdentityService.Domain.Devices;
using IdentityService.Domain.Mfa;
using IdentityService.Domain.Passkeys;
using IdentityService.Domain.Security;
using IdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

/// <summary>
/// EF-Core-Mapping fuer identity_db (docs/adr/0004, Abschnitt 1). Domain-Aggregate bleiben frei
/// von EF-Core-Abhaengigkeiten; Mapping nutzt private Setter + private Konstruktoren per Reflection.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<MfaCredential> MfaCredentials => Set<MfaCredential>();

    public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();

    public DbSet<KnownDevice> KnownDevices => Set<KnownDevice>();

    public DbSet<PendingDeviceConfirmation> PendingDeviceConfirmations => Set<PendingDeviceConfirmation>();

    public DbSet<SecurityPolicy> SecurityPolicies => Set<SecurityPolicy>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("users");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
            builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(200);
            builder.Property(u => u.MfaEnabled).IsRequired();
            builder.Property(u => u.FailedLoginAttempts).IsRequired();
            builder.Property(u => u.LockedUntilUtc);
            builder.Property(u => u.PasswordChangedAtUtc).IsRequired();

            builder.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<MfaCredential>(builder =>
        {
            builder.ToTable("mfa_credentials");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.SecretBase32).IsRequired();
            builder.Property(m => m.Verified).IsRequired();
            builder.Property(m => m.CreatedAtUtc).IsRequired();

            builder.HasIndex(m => m.UserId).IsUnique();
        });

        modelBuilder.Entity<PasskeyCredential>(builder =>
        {
            builder.ToTable("passkey_credentials");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.CredentialId).IsRequired();
            builder.Property(p => p.PublicKey).IsRequired();
            builder.Property(p => p.SignCount).IsRequired();
            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
            builder.Property(p => p.CreatedAtUtc).IsRequired();

            builder.HasIndex(p => p.CredentialId).IsUnique();
            builder.HasIndex(p => p.UserId);
        });

        modelBuilder.Entity<KnownDevice>(builder =>
        {
            builder.ToTable("known_devices");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.FingerprintHash).IsRequired().HasMaxLength(64);
            builder.Property(d => d.UserAgent).IsRequired().HasMaxLength(500);
            builder.Property(d => d.LastIpRange).IsRequired().HasMaxLength(100);
            builder.Property(d => d.IsRevoked).IsRequired();
            builder.Property(d => d.FirstSeenAtUtc).IsRequired();
            builder.Property(d => d.LastSeenAtUtc).IsRequired();

            builder.HasIndex(d => new { d.UserId, d.FingerprintHash }).IsUnique();
        });

        modelBuilder.Entity<PendingDeviceConfirmation>(builder =>
        {
            builder.ToTable("pending_device_confirmations");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.FingerprintHash).IsRequired().HasMaxLength(64);
            builder.Property(p => p.UserAgent).IsRequired().HasMaxLength(500);
            builder.Property(p => p.LastIpRange).IsRequired().HasMaxLength(100);
            builder.Property(p => p.CodeHash).IsRequired().HasMaxLength(64);
            builder.Property(p => p.CreatedAtUtc).IsRequired();
            builder.Property(p => p.ExpiresAtUtc).IsRequired();

            builder.HasIndex(p => p.UserId);
        });

        modelBuilder.Entity<SecurityPolicy>(builder =>
        {
            builder.ToTable("security_policies");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.RequireMfa).IsRequired();
            builder.Property(p => p.PasswordMinLength).IsRequired();
            builder.Property(p => p.PasswordExpiryDays).IsRequired();
            builder.Property(p => p.MaxFailedLoginAttempts).IsRequired();
            builder.Property(p => p.LockoutDurationMinutes).IsRequired();
        });

        modelBuilder.Entity<AuditLogEntry>(builder =>
        {
            builder.ToTable("audit_log_entries");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.OccurredAtUtc).IsRequired();
            builder.Property(a => a.EventType).IsRequired().HasConversion<string>().HasMaxLength(40);
            builder.Property(a => a.ActorUserId).IsRequired();
            builder.Property(a => a.TargetUserId);
            builder.Property(a => a.Details).IsRequired();
            builder.Property(a => a.IpAddress).HasMaxLength(64);

            builder.HasIndex(a => a.OccurredAtUtc);
            builder.HasIndex(a => a.EventType);
            builder.HasIndex(a => a.ActorUserId);
        });
    }
}
