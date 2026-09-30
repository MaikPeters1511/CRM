namespace EmployeeService.Tests.Web;

/// <summary>
/// Regressionstest fuer Tests/E2E/README.md, Bug 2: der geseedete identity-service-Testnutzer
/// "employee@crm.local" muss end-to-end einen echten Employee-Datensatz besitzen, damit der
/// Ownership-Check (JWT NameIdentifier == EmployeeId) im positiven Fall testbar ist.
/// </summary>
public class DevEmployeeSeedHostedServiceTests
{
    [Test]
    public async Task Startup_InDevelopmentEnvironment_SeedsEmployeeWithKnownIdMatchingIdentityServiceTestUser()
    {
        using var factory = new EmployeeServiceDevSeedWebFactory();

        // Der Hosted Service laeuft beim Start des TestServers (WebApplicationFactory startet den Host).
        using var client = factory.CreateClient();

        var expectedEmployeeUserId = Guid.Parse("e573eed0-8c40-749f-f05c-aa6839c81cb4");
        var seededEmployee = factory.Repository.Employees.SingleOrDefault(e => e.Id == expectedEmployeeUserId);

        await Assert.That(seededEmployee).IsNotNull();
        await Assert.That(seededEmployee!.Email).IsEqualTo("employee@crm.local");
    }
}
