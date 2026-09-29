using CustomerService.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Infrastructure.Persistence;

public sealed class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(builder =>
        {
            builder.ToTable("customers");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.FirstName).IsRequired().HasMaxLength(200);
            builder.Property(c => c.LastName).IsRequired().HasMaxLength(200);
            builder.Property(c => c.Email).HasMaxLength(320);
            builder.Property(c => c.Phone).HasMaxLength(50);
            builder.Property(c => c.IsActive).IsRequired();
            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(200);
            builder.Property(c => c.ModifiedAt);
            builder.Property(c => c.ModifiedBy).HasMaxLength(200);

            builder.OwnsOne(c => c.Address, addressBuilder =>
            {
                addressBuilder.Property(a => a.Street).HasColumnName("address_street").HasMaxLength(200);
                addressBuilder.Property(a => a.City).HasColumnName("address_city").HasMaxLength(200);
                addressBuilder.Property(a => a.ZipCode).HasColumnName("address_zip_code").HasMaxLength(20);
                addressBuilder.Property(a => a.Country).HasColumnName("address_country").HasMaxLength(100);
            });

            builder.Navigation(c => c.Address).IsRequired(false);

            builder.HasIndex(c => c.LastName);
            builder.HasIndex(c => c.IsActive);
            builder.Ignore(c => c.DomainEvents);
        });
    }
}
