using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;

namespace EmployeeService.Application.Employees.Create;

public static class CreateEmployeeHandler
{
    public static async Task<CreateEmployeeResult> Handle(
        CreateEmployeeCommand command,
        IEmployeeRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.Email) &&
            await repository.ExistsByEmailAsync(command.Email, cancellationToken))
        {
            return CreateEmployeeResult.EmailConflict();
        }

        var result = Employee.Create(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Role,
            command.CreatedBy,
            timeProvider.GetUtcNow());

        if (!result.IsSuccess)
        {
            return CreateEmployeeResult.ValidationFailed(result.Errors);
        }

        await repository.AddAsync(result.Employee!, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return CreateEmployeeResult.Success(EmployeeDto.FromDomain(result.Employee!));
    }
}
