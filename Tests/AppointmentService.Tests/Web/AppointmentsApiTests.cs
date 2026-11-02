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
    public async Task PostAppointments_WithOffsetLessStartAndEnd_IsInterpretedAsUtcAndReturnsCreated()
    {
        // Tests/E2E/README.md, Bug 9: das reale Angular-Formular sendet start/end OHNE
        // UTC-Offset (z.B. "2026-10-19T10:00:00"). Der Server (System.Text.Json-Default) hat das
        // bisher als lokale Server-Zeitzone statt UTC interpretiert, wodurch das Schreiben nach
        // PostgreSQL ('timestamp with time zone', nur Offset 0 erlaubt) mit 500 abstuerzte.
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");

        var serviceId = Guid.NewGuid();
        var payload = $$"""
            {"customerId":"{{customerId}}","employeeId":"{{employeeId}}","serviceId":"{{serviceId}}",
             "start":"2026-10-19T10:00:00","end":"2026-10-19T11:00:00"}
            """;

        var response = await client.PostAsync(
            "/api/appointments", new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(factory.Repository.Appointments).Count().IsEqualTo(1);
        var persisted = factory.Repository.Appointments.Single();
        await Assert.That(persisted.Start).IsEqualTo(Day.AddHours(10));
        await Assert.That(persisted.Start.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(persisted.End).IsEqualTo(Day.AddHours(11));
        await Assert.That(persisted.End.Offset).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task PutReschedule_WithOffsetLessNewStartAndEnd_IsInterpretedAsUtcAndReturnsOk()
    {
        // Tests/E2E/README.md, Bug 9: identisches Risiko fuer den Reschedule-Endpunkt (selbe
        // Codepfad-Familie, selbe Angular-Komponente baut den Request-Body ohne Offset).
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeId, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        client.DefaultRequestHeaders.Add("X-Test-UserId", employeeId.ToString());

        var createResponse = await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));
        var created = await createResponse.Content.ReadFromJsonAsync<AppointmentService.Application.Appointments.AppointmentDto>(JsonOptions);

        var payload = """
            {"newStart":"2026-10-19T13:00:00","newEnd":"2026-10-19T14:00:00"}
            """;

        var response = await client.PutAsync(
            $"/api/appointments/{created!.Id}/reschedule",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var persisted = factory.Repository.Appointments.Single();
        await Assert.That(persisted.Start).IsEqualTo(Day.AddHours(13));
        await Assert.That(persisted.Start.Offset).IsEqualTo(TimeSpan.Zero);
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
        client.DefaultRequestHeaders.Add("X-Test-UserId", employeeId.ToString());

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
        client.DefaultRequestHeaders.Add("X-Test-UserId", employeeId.ToString());

        var createResponse = await client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeId, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));
        var created = await createResponse.Content.ReadFromJsonAsync<AppointmentService.Application.Appointments.AppointmentDto>(JsonOptions);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{created!.Id}/cancel", new CancelAppointmentRequest("Kunde krank"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(factory.Repository.Appointments.Single().Status)
            .IsEqualTo(AppointmentService.Domain.Appointments.AppointmentStatus.Cancelled);
    }

    [Test]
    public async Task GetCalendar_AsEmployee_ReturnsOnlyOwnAppointmentsEvenWhenQueryingAnotherEmployeeId()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeA = Guid.NewGuid();
        var employeeB = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeA, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        factory.EmployeeAvailabilityClient.SeedSlot(employeeB, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var createdA = await adminClient.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeA, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));
        await Assert.That(createdA.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var createdB = await adminClient.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeB, Guid.NewGuid(), Day.AddHours(13), Day.AddHours(14)));
        await Assert.That(createdB.StatusCode).IsEqualTo(HttpStatusCode.Created);

        using var employeeClient = factory.CreateClient();
        employeeClient.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        employeeClient.DefaultRequestHeaders.Add("X-Test-UserId", employeeA.ToString());

        // Employee A explicitly filters by employee B's id - the server MUST ignore this and only
        // return A's own appointments (terminverwaltung.openspec.md, Abschnitt 3).
        var response = await employeeClient.GetAsync(
            $"/api/appointments?employeeId={employeeB}&from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var calendar = await response.Content.ReadFromJsonAsync<AppointmentService.Web.Controllers.CalendarResponse>(JsonOptions);

        await Assert.That(calendar!.Appointments).Count().IsEqualTo(1);
        await Assert.That(calendar.Appointments[0].EmployeeId).IsEqualTo(employeeA);
    }

    [Test]
    public async Task GetCalendar_AsAdmin_ReturnsAppointmentsOfAllEmployees()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeA = Guid.NewGuid();
        var employeeB = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeA, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));
        factory.EmployeeAvailabilityClient.SeedSlot(employeeB, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        await adminClient.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeA, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));
        await adminClient.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeB, Guid.NewGuid(), Day.AddHours(13), Day.AddHours(14)));

        var response = await adminClient.GetAsync($"/api/appointments?from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var calendar = await response.Content.ReadFromJsonAsync<AppointmentService.Web.Controllers.CalendarResponse>(JsonOptions);

        await Assert.That(calendar!.Appointments).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetCalendar_WithApprovedAbsenceInPeriod_ReturnsItInAbsenceEntriesSeparateFromAppointments()
    {
        using var factory = new AppointmentServiceWebFactory();
        var employeeId = Guid.NewGuid();
        factory.EmployeeAbsenceClient.Seed(
            new AppointmentService.Application.Abstractions.ApprovedAbsenceDto(employeeId, "Vacation", DateOnly.FromDateTime(Day.DateTime), DateOnly.FromDateTime(Day.DateTime)));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync(
            $"/api/appointments?employeeId={employeeId}&from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var calendar = await response.Content.ReadFromJsonAsync<AppointmentService.Web.Controllers.CalendarResponse>(JsonOptions);

        await Assert.That(calendar!.Appointments).IsEmpty();
        await Assert.That(calendar.AbsenceEntries).Count().IsEqualTo(1);
        await Assert.That(calendar.AbsenceEntries[0].EmployeeId).IsEqualTo(employeeId);
        await Assert.That(calendar.AbsenceEntries[0].Type).IsEqualTo("Vacation");
    }

    [Test]
    public async Task PutReschedule_AsEmployeeForAnotherEmployeesAppointment_ReturnsForbiddenAndLeavesAppointmentUnchanged()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeB = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeB, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var createResponse = await adminClient.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeB, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));
        var created = await createResponse.Content.ReadFromJsonAsync<AppointmentService.Application.Appointments.AppointmentDto>(JsonOptions);

        using var employeeClient = factory.CreateClient();
        employeeClient.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        employeeClient.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());

        var response = await employeeClient.PutAsJsonAsync(
            $"/api/appointments/{created!.Id}/reschedule",
            new RescheduleAppointmentRequest(Day.AddHours(13), Day.AddHours(14)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(factory.Repository.Appointments.Single().Start).IsEqualTo(Day.AddHours(10));
    }

    [Test]
    public async Task PostCancel_AsEmployeeForAnotherEmployeesAppointment_ReturnsForbiddenAndLeavesAppointmentUnchanged()
    {
        using var factory = new AppointmentServiceWebFactory();
        var customerId = Guid.NewGuid();
        var employeeB = Guid.NewGuid();
        factory.CustomerVerificationClient.SeedActive(customerId);
        factory.EmployeeAvailabilityClient.SeedSlot(employeeB, new AvailableSlotDto(DateOnly.FromDateTime(Day.DateTime), new TimeOnly(9, 0), new TimeOnly(17, 0)));

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var createResponse = await adminClient.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest(customerId, employeeB, Guid.NewGuid(), Day.AddHours(10), Day.AddHours(11)));
        var created = await createResponse.Content.ReadFromJsonAsync<AppointmentService.Application.Appointments.AppointmentDto>(JsonOptions);

        using var employeeClient = factory.CreateClient();
        employeeClient.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        employeeClient.DefaultRequestHeaders.Add("X-Test-UserId", Guid.NewGuid().ToString());

        var response = await employeeClient.PostAsJsonAsync(
            $"/api/appointments/{created!.Id}/cancel", new CancelAppointmentRequest("Kunde krank"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(factory.Repository.Appointments.Single().Status)
            .IsEqualTo(AppointmentService.Domain.Appointments.AppointmentStatus.Confirmed);
    }
}
