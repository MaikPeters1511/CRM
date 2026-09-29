using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppointmentService.Application.Abstractions;
using AppointmentService.Web.Controllers;

namespace AppointmentService.Tests.Web;

public class AppointmentsApiTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    // Der Web-Host registriert einen JsonStringEnumConverter fuer MVC-Responses (Program.cs);
    // der HttpClient in Tests braucht denselben Converter, um AppointmentDto.Status (enum) zu lesen.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Test]
    public async Task PostAppointments_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new AppointmentServiceWebFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PostAppointments_WithinAvailableWindow_ReturnsCreatedAndPersistsAppointment()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(factory.Repository.Appointments).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PostAppointments_OutsideWorkingHours_ReturnsBadRequestAndPersistsNothing()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeId, Guid.NewGuid(), Day.AddHours(18), Day.AddHours(19)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(factory.Repository.Appointments).IsEmpty();
    }

    [Test]
    public async Task PostAppointments_WithDeactivatedCustomer_ReturnsBadRequest()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId, isActive: false);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var response = await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(factory.Repository.Appointments).IsEmpty();
    }

    [Test]
    public async Task GetCalendar_AsEmployee_ReturnsAppointmentListWithDateAndTimeFields()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));

        var response = await client.GetAsync(
            $"/api/appointments?employeeId={employeeId}&from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("\"date\"");
        await Assert.That(body).Contains("\"startTime\"");
        await Assert.That(body).Contains("\"endTime\"");
    }

    [Test]
    public async Task PostCancel_OnExistingAppointment_ReturnsOkAndMarksCancelled()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var createResponse = await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));
        var created = await createResponse.Content.ReadFromJsonAsync<AppointmentService.Application.Appointments.AppointmentDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{created!.Id}/cancel", new CancelAppointmentRequest("Kunde krank"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(factory.Repository.Appointments.Single().Status)
            .IsEqualTo(AppointmentService.Domain.Appointments.AppointmentStatus.Cancelled);
    }
}
