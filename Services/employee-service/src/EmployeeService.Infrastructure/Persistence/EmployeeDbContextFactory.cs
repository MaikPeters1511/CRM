using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EmployeeService.Infrastructure.Persistence;

public sealed class EmployeeDbContextFactory : IDesignTimeDbContextFactory<EmployeeDbContext>
{
    public EmployeeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EMPLOYEE_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=employee_db;Username=employee_service;Password=design-time-only";

        var optionsBuilder = new DbContextOptionsBuilder<EmployeeDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new EmployeeDbContext(optionsBuilder.Options);
    }
}
