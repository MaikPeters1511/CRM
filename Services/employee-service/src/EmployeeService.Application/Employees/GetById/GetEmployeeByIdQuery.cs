namespace EmployeeService.Application.Employees.GetById;

public sealed record GetEmployeeByIdQuery(Guid EmployeeId);

public sealed record GetEmployeeByIdResult(EmployeeDto? Employee)
{
    public bool Found => Employee is not null;
}
