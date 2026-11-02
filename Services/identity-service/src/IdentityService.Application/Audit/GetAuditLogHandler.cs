namespace IdentityService.Application.Audit;

public sealed record GetAuditLogQuery(AuditLogFilter Filter, int Page, int PageSize);

public sealed class GetAuditLogHandler(IAuditLogRepository repository)
{
    public Task<AuditLogPage> HandleAsync(GetAuditLogQuery query, CancellationToken cancellationToken) =>
        repository.QueryAsync(query.Filter, query.Page, query.PageSize, cancellationToken);
}
