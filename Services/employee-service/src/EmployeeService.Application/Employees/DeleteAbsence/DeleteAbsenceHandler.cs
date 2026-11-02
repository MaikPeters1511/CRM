using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.DeleteAbsence;

public static class DeleteAbsenceHandler
{
    public static async Task<DeleteAbsenceResult> Handle(
        DeleteAbsenceCommand command,
        IEmployeeRepository repository,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return DeleteAbsenceResult.NotFound();
        }

        var removed = employee.RemoveAbsence(command.AbsenceId, command.RemovedBy, DateTimeOffset.UtcNow);
        if (!removed)
        {
            return DeleteAbsenceResult.NotFound();
        }

        await repository.SaveChangesAsync(cancellationToken);

        return DeleteAbsenceResult.Success();
    }
}
