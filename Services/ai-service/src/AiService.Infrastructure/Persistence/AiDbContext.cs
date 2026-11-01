using AiService.Domain.Chat;
using Microsoft.EntityFrameworkCore;

namespace AiService.Infrastructure.Persistence;

/// <summary>
/// Eigene, schlanke PostgreSQL-Datenbank <c>ai_db</c> - ausschliesslich fuer Chatverlauf/
/// Session-Metadaten (ki-terminassistent.openspec.md, Abschnitt 7). KEINE Termin-/Kunden-/
/// Leistungsstammdaten - die eigentliche Datengrundlage bleibt Qdrant (Ableitungsdaten) plus die
/// synchronen Live-Calls an appointment-service/employee-service/catalog-service/customer-service.
/// </summary>
public sealed class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatSession>(builder =>
        {
            builder.ToTable("chat_sessions");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.OwnerUserId).IsRequired().HasMaxLength(200);
            builder.Property(s => s.CreatedAt).IsRequired();

            builder.HasIndex(s => s.OwnerUserId);

            builder.HasMany(s => s.Messages)
                .WithOne()
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(s => s.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ChatMessage>(builder =>
        {
            builder.ToTable("chat_messages");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.SessionId).IsRequired();
            builder.Property(m => m.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
            builder.Property(m => m.Content).IsRequired();
            builder.Property(m => m.CreatedAt).IsRequired();

            builder.HasIndex(m => m.SessionId);
        });
    }
}
