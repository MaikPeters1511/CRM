using AppointmentService.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace AppointmentService.Infrastructure.Persistence;

public sealed class AppointmentDbContext(DbContextOptions<AppointmentDbContext> options) : DbContext(options)
{
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(builder =>
        {
            builder.ToTable("appointments");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.CustomerId).IsRequired();
            builder.Property(a => a.EmployeeId).IsRequired();
            builder.Property(a => a.ServiceId).IsRequired();
            builder.Property(a => a.Start).IsRequired();
            builder.Property(a => a.End).IsRequired();
            builder.Property(a => a.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.CancellationReason).HasMaxLength(500);
            builder.Property(a => a.ReminderSentAt);
            builder.Property(a => a.CreatedAt).IsRequired();
            builder.Property(a => a.CreatedBy).IsRequired().HasMaxLength(200);
            builder.Property(a => a.ModifiedAt);
            builder.Property(a => a.ModifiedBy).HasMaxLength(200);

            builder.HasIndex(a => a.EmployeeId);
            builder.HasIndex(a => a.CustomerId);
            builder.HasIndex(a => a.Start);
            builder.Ignore(a => a.DomainEvents);
        });
    }
}
