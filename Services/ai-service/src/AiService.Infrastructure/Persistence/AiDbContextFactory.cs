using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiService.Infrastructure.Persistence;

public sealed class AiDbContextFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AI_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=ai_db;Username=ai_service;Password=design-time-only";

        var optionsBuilder = new DbContextOptionsBuilder<AiDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AiDbContext(optionsBuilder.Options);
    }
}
