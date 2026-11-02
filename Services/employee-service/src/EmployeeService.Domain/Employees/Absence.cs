using EmployeeService.Domain.Common;

namespace EmployeeService.Domain.Employees;

public sealed class Absence : Entity
{
    public AbsenceType Type { get; private set; }
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    public AbsenceStatus Status { get; private set; }
    public string RequestedBy { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; private set; }
    public string? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    private Absence()
    {
    }

    public Absence(Guid id, AbsenceType type, DateOnly from, DateOnly to, string requestedBy, DateTimeOffset requestedAt) : base(id)
    {
        if (from > to)
        {
            throw new ArgumentException("From must be on or before To.", nameof(from));
        }

        Type = type;
        From = from;
        To = to;
        Status = AbsenceStatus.Pending;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
    }

    public bool Overlaps(DateOnly from, DateOnly to) => From <= to && To >= from;

    /// <summary>
    /// Genehmigt eine ausstehende Abwesenheitsanfrage (abwesenheit-workflow.openspec.md, Scenario
    /// "Admin genehmigt eine ausstehende Anfrage"). Guard gegen bereits entschiedene Anfragen
    /// (Scenario "Bereits entschiedene Anfrage erneut entscheiden") - liefert einen Fehler statt
    /// eine Exception zu werfen, analog zum bestehenden <see cref="AbsenceMutationResult"/>-Muster.
    /// </summary>
    public AbsenceMutationResult Approve(string decidedBy, DateTimeOffset decidedAt)
    {
        if (Status != AbsenceStatus.Pending)
        {
            return AbsenceMutationResult.Failure(["Absence request has already been decided."]);
        }

        Status = AbsenceStatus.Approved;
        DecidedBy = decidedBy;
        DecidedAt = decidedAt;

        return AbsenceMutationResult.Success(this);
    }

    /// <summary>Lehnt eine ausstehende Abwesenheitsanfrage ab. Guard analog <see cref="Approve"/>.</summary>
    public AbsenceMutationResult Reject(string decidedBy, DateTimeOffset decidedAt)
    {
        if (Status != AbsenceStatus.Pending)
        {
            return AbsenceMutationResult.Failure(["Absence request has already been decided."]);
        }

        Status = AbsenceStatus.Rejected;
        DecidedBy = decidedBy;
        DecidedAt = decidedAt;

        return AbsenceMutationResult.Success(this);
    }
}
