using AppointmentService.Application.Abstractions;

namespace AppointmentService.Tests.Support;

public sealed class FakeEmployeeAbsenceClient : IEmployeeAbsenceClient
{
    private readonly List<ApprovedAbsenceDto> _absences = [];

    public void Seed(ApprovedAbsenceDto absence) => _absences.Add(absence);

    public Task<IReadOnlyList<ApprovedAbsenceDto>> GetApprovedAbsencesAsync(
        Guid? employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApprovedAbsenceDto> result = _absences
            .Where(a => (!employeeId.HasValue || a.EmployeeId == employeeId.Value) && a.To >= from && a.From <= to)
            .ToList();

        return Task.FromResult(result);
    }
}
