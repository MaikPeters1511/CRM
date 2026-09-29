using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AppointmentService.Infrastructure.Persistence;

public sealed class AppointmentDbContextFactory : IDesignTimeDbContextFactory<AppointmentDbContext>
{
    public AppointmentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("APPOINTMENT_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=appointment_db;Username=appointment_service;Password=design-time-only";

        var optionsBuilder = new DbContextOptionsBuilder<AppointmentDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppointmentDbContext(optionsBuilder.Options);
    }
}
