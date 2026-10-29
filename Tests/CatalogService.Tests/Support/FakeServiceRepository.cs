using CatalogService.Application.Abstractions;
using CatalogService.Domain.Services;

namespace CatalogService.Tests.Support;

public sealed class FakeServiceRepository : IServiceRepository
{
    private readonly List<Service> _services = [];

    public IReadOnlyList<Service> Services => _services;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(Service service) => _services.Add(service);

    public Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_services.FirstOrDefault(s => s.Id == id));
    }

    public Task AddAsync(Service service, CancellationToken cancellationToken)
    {
        _services.Add(service);
        return Task.CompletedTask;
    }

    public Task<ServiceSearchResult> SearchAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IEnumerable<Service> query = _services;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        var all = query.ToList();
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new ServiceSearchResult(paged, all.Count));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
