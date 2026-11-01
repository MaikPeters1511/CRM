namespace AiService.Application.Abstractions;

/// <summary>
/// Synchroner, lesender Aufruf gegen <c>catalog-service</c> (<c>GET /api/services?search=</c>) -
/// loest Freitext-Leistungsbegriffe aus der Chat-Anfrage auf konkrete <c>ServiceId</c>s auf.
/// </summary>
public interface ICatalogQueryClient
{
    Task<IReadOnlyList<ServiceSummaryDto>> SearchActiveServicesAsync(
        string searchTerm, CancellationToken cancellationToken);
}

public sealed record ServiceSummaryDto(Guid ServiceId, string Name, string? Description, int? DefaultDurationMinutes);
