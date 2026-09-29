using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.SetWorkingHours;

public static class SetWorkingHoursHandler
{
    public static async Task<SetWorkingHoursResult> Handle(
        SetWorkingHoursCommand command,
        IEmployeeRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return SetWorkingHoursResult.NotFound();
        }

        var slots = command.Slots
            .Select(s => (s.Weekday, s.StartTime, s.EndTime))
            .ToList();

        var result = employee.SetWorkingHours(slots, command.ModifiedBy, timeProvider.GetUtcNow());

        if (!result.IsSuccess)
        {
            return SetWorkingHoursResult.ValidationFailed(result.Errors);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return SetWorkingHoursResult.Success(EmployeeDto.FromDomain(employee));
    }
}
