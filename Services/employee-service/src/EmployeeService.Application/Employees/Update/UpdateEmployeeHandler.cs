using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Update;

public static class UpdateEmployeeHandler
{
    public static async Task<UpdateEmployeeResult> Handle(
        UpdateEmployeeCommand command,
        IEmployeeRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(command.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return UpdateEmployeeResult.NotFound();
        }

        var now = timeProvider.GetUtcNow();

        var result = employee.Update(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Role,
            command.ModifiedBy,
            now);

        if (!result.IsSuccess)
        {
            return UpdateEmployeeResult.ValidationFailed(result.Errors);
        }

        if (command.Services is not null)
        {
            employee.ReplaceServices(
                command.Services.Select(s => new EmployeeServiceAssignment(s.ServiceId, s.ServiceName)),
                command.ModifiedBy,
                now);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return UpdateEmployeeResult.Success(EmployeeDto.FromDomain(employee));
    }
}
