using System.Net;
using System.Net.Http.Json;
using CustomerService.Application.Customers.Create;
using CustomerService.Domain.Customers;

namespace CustomerService.Tests.Web;

public class CustomersApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PostCustomers_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CustomerServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Max", "Mustermann", "max@example.com", null, null, "ignored"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PostCustomers_AsEmployeeWithValidData_ReturnsCreatedAndPersistsCustomer()
    {
        using var factory = new CustomerServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsJsonAsync("/api/customers",
            new { FirstName = "Max", LastName = "Mustermann", Email = "max@example.com", Phone = (string?)null, Address = (object?)null });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(factory.Repository.Customers).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PostCustomers_AsEmployeeWithoutRequiredFields_ReturnsValidationProblem()
    {
        using var factory = new CustomerServiceWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsJsonAsync("/api/customers",
            new { FirstName = "", LastName = "", Email = (string?)null, Phone = (string?)null, Address = (object?)null });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(factory.Repository.Customers).IsEmpty();
    }

    [Test]
    public async Task PostDeactivate_AsEmployee_ReturnsForbidden()
    {
        using var factory = new CustomerServiceWebFactory();
        var customer = Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;
        factory.Repository.Seed(customer);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsync($"/api/customers/{customer.Id}/deactivate", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task PostDeactivate_AsAdmin_DeactivatesCustomer()
    {
        using var factory = new CustomerServiceWebFactory();
        var customer = Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!;
        factory.Repository.Seed(customer);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.PostAsync($"/api/customers/{customer.Id}/deactivate", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(customer.IsActive).IsFalse();
    }

    [Test]
    public async Task GetCustomers_AsEmployeeSearchingByLastName_ReturnsOnlyMatchingCustomers()
    {
        using var factory = new CustomerServiceWebFactory();
        factory.Repository.Seed(Customer.Create("Max", "Mustermann", "max@example.com", null, null, "employee-1", Now).Customer!);
        factory.Repository.Seed(Customer.Create("Otto", "Schmidt", "otto@example.com", null, null, "employee-1", Now).Customer!);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.GetAsync("/api/customers?search=Muster");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Mustermann");
        await Assert.That(body).DoesNotContain("Schmidt");
    }
}
