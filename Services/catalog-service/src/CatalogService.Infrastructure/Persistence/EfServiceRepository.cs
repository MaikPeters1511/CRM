using CatalogService.Application.Abstractions;
using CatalogService.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure.Persistence;

public sealed class EfServiceRepository(CatalogDbContext dbContext) : IServiceRepository
{
    public async Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Services.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task AddAsync(Service service, CancellationToken cancellationToken)
    {
        await dbContext.Services.AddAsync(service, cancellationToken);
    }

    public async Task<ServiceSearchResult> SearchAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Service> query = dbContext.Services.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{search}%"));
        }

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ServiceSearchResult(items, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
