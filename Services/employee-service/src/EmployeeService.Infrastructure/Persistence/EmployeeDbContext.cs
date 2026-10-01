using EmployeeService.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Infrastructure.Persistence;

public sealed class EmployeeDbContext(DbContextOptions<EmployeeDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(builder =>
        {
            builder.ToTable("employees");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.FirstName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.LastName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Email).IsRequired().HasMaxLength(320);
            builder.Property(e => e.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.IsActive).IsRequired();
            builder.Property(e => e.CreatedAt).IsRequired();
            builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(200);
            builder.Property(e => e.ModifiedAt);
            builder.Property(e => e.ModifiedBy).HasMaxLength(200);

            builder.HasIndex(e => e.Email).IsUnique();
            builder.HasIndex(e => e.LastName);
            builder.HasIndex(e => e.IsActive);
            builder.Ignore(e => e.DomainEvents);

            builder.OwnsMany(e => e.Services, servicesBuilder =>
            {
                servicesBuilder.ToTable("employee_services");
                servicesBuilder.WithOwner().HasForeignKey("EmployeeId");
                servicesBuilder.Property<Guid>("EmployeeId");
                servicesBuilder.HasKey("EmployeeId", nameof(EmployeeServiceAssignment.ServiceId));
                servicesBuilder.Property(s => s.ServiceId).HasColumnName("ServiceId");
                servicesBuilder.Property(s => s.ServiceName).HasColumnName("ServiceName").IsRequired().HasMaxLength(200);
            });

            builder.Navigation(e => e.Services).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.OwnsMany(e => e.WorkingHours, workingHoursBuilder =>
            {
                workingHoursBuilder.ToTable("employee_working_hours");
                workingHoursBuilder.WithOwner().HasForeignKey("EmployeeId");
                workingHoursBuilder.Property<Guid>("EmployeeId");
                workingHoursBuilder.HasKey(w => w.Id);
                // Tests/E2E/README.md, Bug 6: Id wird client-seitig per Guid.NewGuid() gesetzt
                // (siehe Employee.SetWorkingHours), nicht von der Datenbank generiert. Ohne
                // ValueGeneratedNever() interpretiert EF Core den bereits gesetzten Guid-Wert als
                // "Entity existiert schon" (IsKeySet-Heuristik) und generiert faelschlich ein
                // UPDATE statt eines INSERT -> DbUpdateConcurrencyException beim ersten Speichern.
                workingHoursBuilder.Property(w => w.Id).ValueGeneratedNever();
                workingHoursBuilder.Property(w => w.Weekday).IsRequired().HasConversion<string>().HasMaxLength(20);
                workingHoursBuilder.Property(w => w.StartTime).IsRequired();
                workingHoursBuilder.Property(w => w.EndTime).IsRequired();
            });

            builder.Navigation(e => e.WorkingHours).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.OwnsMany(e => e.Absences, absencesBuilder =>
            {
                absencesBuilder.ToTable("employee_absences");
                absencesBuilder.WithOwner().HasForeignKey("EmployeeId");
                absencesBuilder.Property<Guid>("EmployeeId");
                absencesBuilder.HasKey(a => a.Id);
                // Identisches Muster/Ursache wie bei WorkingHours.Id oben (Tests/E2E/README.md, Bug 6) -
                // client-seitig per Guid.NewGuid() gesetzter Surrogatschluessel ohne ValueGeneratedNever()
                // fuehrt zum selben faelschlichen UPDATE-statt-INSERT-Verhalten.
                absencesBuilder.Property(a => a.Id).ValueGeneratedNever();
                absencesBuilder.Property(a => a.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
                absencesBuilder.Property(a => a.From).IsRequired();
                absencesBuilder.Property(a => a.To).IsRequired();
            });

            builder.Navigation(e => e.Absences).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
