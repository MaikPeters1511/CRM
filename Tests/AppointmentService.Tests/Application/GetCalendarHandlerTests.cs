using AppointmentService.Application.Abstractions;
using AppointmentService.Application.Appointments.Calendar;
using AppointmentService.Domain.Appointments;
using AppointmentService.Tests.Support;

namespace AppointmentService.Tests.Application;

public class GetCalendarHandlerTests
{
    private static readonly TimeProvider Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Handle_WithMultipleAppointmentsInWeek_ReturnsThemOrderedByStartWithDateAndTimeFields()
    {
        var repository = new FakeAppointmentRepository();
        var employeeId = Guid.NewGuid();

        var later = Appointment.Create(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Monday.AddHours(14), Monday.AddHours(15),
            [new AvailabilityWindow(Monday.AddHours(9), Monday.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        var earlier = Appointment.Create(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Monday.AddHours(10), Monday.AddHours(11),
            [new AvailabilityWindow(Monday.AddHours(9), Monday.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        repository.Seed(later);
        repository.Seed(earlier);

        var query = new GetCalendarQuery(employeeId, null, DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.AddDays(6).DateTime), IsAdmin: true, CurrentEmployeeId: null);

        var result = await GetCalendarHandler.Handle(query, repository, new FakeEmployeeAbsenceClient(), CancellationToken.None);

        await Assert.That(result.Appointments).Count().IsEqualTo(2);
        await Assert.That(result.Appointments[0].Id).IsEqualTo(earlier.Id);
        await Assert.That(result.Appointments[0].Date).IsEqualTo(DateOnly.FromDateTime(Monday.DateTime));
        await Assert.That(result.Appointments[0].StartTime).IsEqualTo(new TimeOnly(10, 0));
        await Assert.That(result.Appointments[0].EndTime).IsEqualTo(new TimeOnly(11, 0));
        await Assert.That(result.Appointments[1].Id).IsEqualTo(later.Id);
    }

    [Test]
    public async Task Handle_ExcludesCancelledAppointmentsByDefault_SoTheSlotIsFreeForEmployeeServiceAvailability()
    {
        var repository = new FakeAppointmentRepository();
        var employeeId = Guid.NewGuid();

        var appointment = Appointment.Create(
            Guid.NewGuid(), employeeId, Guid.NewGuid(), Monday.AddHours(10), Monday.AddHours(11),
            [new AvailabilityWindow(Monday.AddHours(9), Monday.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        appointment.Cancel("Grund", "employee-1", Clock.GetUtcNow());
        repository.Seed(appointment);

        var query = new GetCalendarQuery(employeeId, null, DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.DateTime), IsAdmin: true, CurrentEmployeeId: null);

        var result = await GetCalendarHandler.Handle(query, repository, new FakeEmployeeAbsenceClient(), CancellationToken.None);

        await Assert.That(result.Appointments).IsEmpty();
    }

    [Test]
    public async Task Handle_AsEmployee_ReturnsOnlyOwnAppointmentsEvenWhenEmployeeIdFilterTargetsAnotherEmployee()
    {
        var repository = new FakeAppointmentRepository();
        var employeeA = Guid.NewGuid();
        var employeeB = Guid.NewGuid();

        var ownAppointment = Appointment.Create(
            Guid.NewGuid(), employeeA, Guid.NewGuid(), Monday.AddHours(10), Monday.AddHours(11),
            [new AvailabilityWindow(Monday.AddHours(9), Monday.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        var foreignAppointment = Appointment.Create(
            Guid.NewGuid(), employeeB, Guid.NewGuid(), Monday.AddHours(10), Monday.AddHours(11),
            [new AvailabilityWindow(Monday.AddHours(9), Monday.AddHours(17))], [], "employee-2", Clock.GetUtcNow()).Appointment!;
        repository.Seed(ownAppointment);
        repository.Seed(foreignAppointment);

        // Employee A tries to read employee B's appointments via the employeeId query parameter -
        // the server MUST ignore this and only return A's own appointments.
        var query = new GetCalendarQuery(
            employeeB, null, DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.DateTime),
            IsAdmin: false, CurrentEmployeeId: employeeA);

        var result = await GetCalendarHandler.Handle(query, repository, new FakeEmployeeAbsenceClient(), CancellationToken.None);

        await Assert.That(result.Appointments).Count().IsEqualTo(1);
        await Assert.That(result.Appointments[0].Id).IsEqualTo(ownAppointment.Id);
    }

    [Test]
    public async Task Handle_AsAdmin_ReturnsAppointmentsOfAllEmployees()
    {
        var repository = new FakeAppointmentRepository();
        var employeeA = Guid.NewGuid();
        var employeeB = Guid.NewGuid();

        var appointmentA = Appointment.Create(
            Guid.NewGuid(), employeeA, Guid.NewGuid(), Monday.AddHours(10), Monday.AddHours(11),
            [new AvailabilityWindow(Monday.AddHours(9), Monday.AddHours(17))], [], "employee-1", Clock.GetUtcNow()).Appointment!;
        var appointmentB = Appointment.Create(
            Guid.NewGuid(), employeeB, Guid.NewGuid(), Monday.AddHours(13), Monday.AddHours(14),
            [new AvailabilityWindow(Monday.AddHours(9), Monday.AddHours(17))], [], "employee-2", Clock.GetUtcNow()).Appointment!;
        repository.Seed(appointmentA);
        repository.Seed(appointmentB);

        var query = new GetCalendarQuery(
            null, null, DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.DateTime),
            IsAdmin: true, CurrentEmployeeId: null);

        var result = await GetCalendarHandler.Handle(query, repository, new FakeEmployeeAbsenceClient(), CancellationToken.None);

        await Assert.That(result.Appointments).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Handle_WithApprovedAbsenceInPeriod_ReturnsItAsSeparateAbsenceEntry()
    {
        var repository = new FakeAppointmentRepository();
        var employeeId = Guid.NewGuid();
        var absenceClient = new FakeEmployeeAbsenceClient();
        absenceClient.Seed(new ApprovedAbsenceDto(employeeId, "Vacation", new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22)));

        var query = new GetCalendarQuery(
            employeeId, null, DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.AddDays(6).DateTime),
            IsAdmin: true, CurrentEmployeeId: null);

        var result = await GetCalendarHandler.Handle(query, repository, absenceClient, CancellationToken.None);

        await Assert.That(result.Appointments).IsEmpty();
        await Assert.That(result.AbsenceEntries).Count().IsEqualTo(1);
        await Assert.That(result.AbsenceEntries[0].EmployeeId).IsEqualTo(employeeId);
        await Assert.That(result.AbsenceEntries[0].Type).IsEqualTo("Vacation");
        await Assert.That(result.AbsenceEntries[0].From).IsEqualTo(new DateOnly(2026, 10, 20));
    }

    [Test]
    public async Task Handle_AsEmployeeWithoutNameIdentifierClaim_ReturnsNoAppointmentsAndNoAbsences()
    {
        var repository = new FakeAppointmentRepository();
        var absenceClient = new FakeEmployeeAbsenceClient();
        absenceClient.Seed(new ApprovedAbsenceDto(Guid.NewGuid(), "Vacation", DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.DateTime)));

        var query = new GetCalendarQuery(
            null, null, DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.DateTime),
            IsAdmin: false, CurrentEmployeeId: null);

        var result = await GetCalendarHandler.Handle(query, repository, absenceClient, CancellationToken.None);

        await Assert.That(result.Appointments).IsEmpty();
        await Assert.That(result.AbsenceEntries).IsEmpty();
    }
}
