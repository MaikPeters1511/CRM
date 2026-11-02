using IdentityService.Application.Auth;
using IdentityService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class EfUserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
