using System.Globalization;
using System.Text;

namespace IdentityService.Application.Audit;

public sealed record ExportAuditLogCsvQuery(AuditLogFilter Filter);

/// <summary>Exportiert das gefilterte Audit-Log als CSV (identity-audit-log.openspec.md).</summary>
public sealed class ExportAuditLogCsvHandler(IAuditLogRepository repository)
{
    public async Task<string> HandleAsync(ExportAuditLogCsvQuery query, CancellationToken cancellationToken)
    {
        var entries = await repository.QueryAllAsync(query.Filter, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("OccurredAtUtc,EventType,ActorUserId,TargetUserId,Details,IpAddress");

        foreach (var entry in entries)
        {
            csv.AppendLine(string.Join(',',
                entry.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
                entry.EventType,
                entry.ActorUserId,
                entry.TargetUserId?.ToString() ?? string.Empty,
                EscapeCsvField(entry.Details),
                entry.IpAddress ?? string.Empty));
        }

        return csv.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (!field.Contains(',') && !field.Contains('"') && !field.Contains('\n'))
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
