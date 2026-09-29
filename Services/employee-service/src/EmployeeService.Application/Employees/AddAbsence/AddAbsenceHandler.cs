using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.AddAbsence;

public static class AddAbsenceHandler
{
    public static async Task<AddAbsenceResult> Handle(
        AddAbsenceCommand command,
        IEmployeeRepository repository,
        IAppointmentAvailabilityClient appointmentAvailabilityClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return AddAbsenceResult.NotFound();
        }

        if (command.From > command.To)
        {
            return AddAbsenceResult.ValidationFailed(["From must be on or before To."]);
        }

        if (!command.ConfirmOverride)
        {
            var existingAppointments = await appointmentAvailabilityClient.GetAppointmentsAsync(
                command.EmployeeId, command.From, command.To, cancellationToken);

            if (existingAppointments.Count > 0)
            {
                return AddAbsenceResult.Conflict(existingAppointments);
            }
        }

        var result = employee.AddAbsence(command.Type, command.From, command.To, command.AddedBy, timeProvider.GetUtcNow());

        if (!result.IsSuccess)
        {
            return AddAbsenceResult.ValidationFailed(result.Errors);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return AddAbsenceResult.Success(new AbsenceDto(result.Absence!.Id, result.Absence.Type, result.Absence.From, result.Absence.To));
    }
}
