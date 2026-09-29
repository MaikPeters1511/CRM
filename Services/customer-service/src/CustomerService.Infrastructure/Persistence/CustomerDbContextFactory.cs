using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CustomerService.Infrastructure.Persistence;

public sealed class CustomerDbContextFactory : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CUSTOMER_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=customer_db;Username=customer_service;Password=design-time-only";

        var optionsBuilder = new DbContextOptionsBuilder<CustomerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CustomerDbContext(optionsBuilder.Options);
    }
}
