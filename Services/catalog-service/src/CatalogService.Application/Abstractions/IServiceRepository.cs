using CatalogService.Domain.Services;

namespace CatalogService.Application.Abstractions;

public interface IServiceRepository
{
    Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Service service, CancellationToken cancellationToken);

    Task<ServiceSearchResult> SearchAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record ServiceSearchResult(IReadOnlyList<Service> Items, int TotalCount);
