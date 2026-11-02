using EmployeeService.Domain.Common;

namespace EmployeeService.Domain.Notifications;

/// <summary>
/// Eigenstaendiges Aggregate (abwesenheit-workflow.openspec.md, Abschnitt 5/7) - bewusst NICHT Teil
/// des <c>Employee</c>-Aggregats, da Benachrichtigungen einen unabhaengigen Lebenszyklus haben
/// (werden z.B. unabhaengig vom Mitarbeiter-Datensatz als gelesen markiert/geloescht).
/// </summary>
public sealed class Notification : Entity
{
    public Guid RecipientEmployeeId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Notification()
    {
    }

    private Notification(Guid id, Guid recipientEmployeeId, string text, string? link, DateTimeOffset createdAt) : base(id)
    {
        RecipientEmployeeId = recipientEmployeeId;
        Text = text;
        Link = link;
        IsRead = false;
        CreatedAt = createdAt;
    }

    public static Notification Create(Guid id, Guid recipientEmployeeId, string text, string? link, DateTimeOffset createdAt) =>
        new(id, recipientEmployeeId, text, link, createdAt);

    /// <summary>Markiert die Benachrichtigung als gelesen (Scenario "Benachrichtigung als gelesen markieren"). Idempotent.</summary>
    public void MarkAsRead()
    {
        IsRead = true;
    }
}
