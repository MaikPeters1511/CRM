using IdentityService.Application.Audit;
using IdentityService.Domain.Audit;
using NSubstitute;

namespace IdentityService.Tests.Application;

/// <summary>CSV-Export des Audit-Logs (identity-audit-log.openspec.md).</summary>
public class ExportAuditLogCsvHandlerTests
{
    [Test]
    public async Task HandleAsync_WritesHeaderAndOneRowPerEntry()
    {
        var repository = Substitute.For<IAuditLogRepository>();
        var entry = new AuditLogEntry(
            Guid.NewGuid(), AuditEventType.LoginSucceeded, Guid.NewGuid(), null, "Login erfolgreich.", "203.0.113.1");
        repository.QueryAllAsync(Arg.Any<AuditLogFilter>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditLogEntry> { entry });

        var handler = new ExportAuditLogCsvHandler(repository);

        var csv = await handler.HandleAsync(
            new ExportAuditLogCsvQuery(new AuditLogFilter(null, null, null, null)), CancellationToken.None);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines[0]).IsEqualTo("OccurredAtUtc,EventType,ActorUserId,TargetUserId,Details,IpAddress");
        await Assert.That(lines[1]).Contains(entry.ActorUserId.ToString());
        await Assert.That(lines[1]).Contains("LoginSucceeded");
    }

    [Test]
    public async Task HandleAsync_EscapesFieldsContainingCommas()
    {
        var repository = Substitute.For<IAuditLogRepository>();
        var entry = new AuditLogEntry(
            Guid.NewGuid(), AuditEventType.SecurityPolicyChanged, Guid.NewGuid(), null,
            "RequireMfa=true, PasswordMinLength=8", null);
        repository.QueryAllAsync(Arg.Any<AuditLogFilter>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditLogEntry> { entry });

        var handler = new ExportAuditLogCsvHandler(repository);

        var csv = await handler.HandleAsync(
            new ExportAuditLogCsvQuery(new AuditLogFilter(null, null, null, null)), CancellationToken.None);

        await Assert.That(csv).Contains("\"RequireMfa=true, PasswordMinLength=8\"");
    }
}
