using CatalogService.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Service> Services => Set<Service>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Service>(builder =>
        {
            builder.ToTable("services");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
            builder.Property(s => s.Description).HasMaxLength(1000);
            builder.Property(s => s.DefaultDurationMinutes);
            builder.Property(s => s.IsActive).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.CreatedBy).IsRequired().HasMaxLength(200);
            builder.Property(s => s.ModifiedAt);
            builder.Property(s => s.ModifiedBy).HasMaxLength(200);

            builder.HasIndex(s => s.Name);
            builder.HasIndex(s => s.IsActive);
        });
    }
}
