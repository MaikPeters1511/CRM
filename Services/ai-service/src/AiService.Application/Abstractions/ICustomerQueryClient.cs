namespace AiService.Application.Abstractions;

/// <summary>
/// Synchroner, lesender Aufruf gegen <c>customer-service</c> (<c>GET /api/customers?search=</c>) -
/// ausschliesslich zur Namensauflösung ("Frau Müller" → <c>CustomerId</c>), NICHT zur
/// Verfügbarkeitsberechnung (Requirement "Datengrundlage für Vorschläge") und ohne Historie-Details
/// (Requirement "Datenminimierung bei externen LLM-Aufrufen").
/// </summary>
public interface ICustomerQueryClient
{
    Task<IReadOnlyList<CustomerSummaryDto>> SearchByNameAsync(string nameHint, CancellationToken cancellationToken);
}

public sealed record CustomerSummaryDto(Guid CustomerId, string FullName);
