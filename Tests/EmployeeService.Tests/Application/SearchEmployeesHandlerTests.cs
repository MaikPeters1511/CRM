using EmployeeService.Application.Employees.Search;
using EmployeeService.Domain.Employees;
using EmployeeService.Tests.Support;

namespace EmployeeService.Tests.Application;

public class SearchEmployeesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_FilteringByServiceId_ReturnsOnlyEmployeesAssignedToThatService()
    {
        var repository = new FakeEmployeeRepository();
        var serviceId = Guid.NewGuid();

        var withService = Employee.Create("Max", "Mustermann", "max@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!;
        withService.AssignService(serviceId, "Beratung");
        repository.Seed(withService);

        repository.Seed(Employee.Create("Otto", "Schmidt", "otto@example.com", EmployeeRole.Employee, "admin-1", Now).Employee!);

        var query = new SearchEmployeesQuery(Search: null, Role: null, ServiceId: serviceId);
        var result = await SearchEmployeesHandler.Handle(query, repository, CancellationToken.None);

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Items[0].LastName).IsEqualTo("Mustermann");
    }
}
