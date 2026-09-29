using EmployeeService.Application.Abstractions;

namespace EmployeeService.Application.Employees.GetById;

public static class GetEmployeeByIdHandler
{
    public static async Task<GetEmployeeByIdResult> Handle(
        GetEmployeeByIdQuery query,
        IEmployeeRepository repository,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetByIdAsync(query.EmployeeId, cancellationToken);

        return new GetEmployeeByIdResult(employee is null ? null : EmployeeDto.FromDomain(employee));
    }
}
