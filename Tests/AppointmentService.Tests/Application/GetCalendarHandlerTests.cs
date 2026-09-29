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

        var query = new GetCalendarQuery(employeeId, null, DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.AddDays(6).DateTime));

        var result = await GetCalendarHandler.Handle(query, repository, CancellationToken.None);

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

        var query = new GetCalendarQuery(employeeId, null, DateOnly.FromDateTime(Monday.DateTime), DateOnly.FromDateTime(Monday.DateTime));

        var result = await GetCalendarHandler.Handle(query, repository, CancellationToken.None);

        await Assert.That(result.Appointments).IsEmpty();
    }
}
