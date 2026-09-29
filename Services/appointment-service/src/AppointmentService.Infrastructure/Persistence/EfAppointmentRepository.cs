using AppointmentService.Application.Abstractions;
using AppointmentService.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace AppointmentService.Infrastructure.Persistence;

public sealed class EfAppointmentRepository(AppointmentDbContext dbContext) : IAppointmentRepository
{
    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        await dbContext.Appointments.AddAsync(appointment, cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetOverlappingForEmployeeAsync(
        Guid employeeId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Appointments
            .Where(a => a.EmployeeId == employeeId)
            .Where(a => a.Status != AppointmentStatus.Cancelled)
            .Where(a => a.Start < end && start < a.End);

        if (excludeAppointmentId.HasValue)
        {
            query = query.Where(a => a.Id != excludeAppointmentId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> SearchAsync(
        Guid? employeeId,
        Guid? customerId,
        DateOnly from,
        DateOnly to,
        bool includeCancelled,
        CancellationToken cancellationToken)
    {
        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rangeEnd = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        IQueryable<Appointment> query = dbContext.Appointments.AsNoTracking()
            .Where(a => a.Start <= rangeEnd && a.End >= rangeStart);

        if (employeeId.HasValue)
        {
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(a => a.CustomerId == customerId.Value);
        }

        if (!includeCancelled)
        {
            query = query.Where(a => a.Status != AppointmentStatus.Cancelled);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetFutureConfirmedByCustomerAsync(
        Guid customerId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        return await dbContext.Appointments
            .Where(a => a.CustomerId == customerId)
            .Where(a => a.Status == AppointmentStatus.Confirmed)
            .Where(a => a.Start > asOf)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetUpcomingConfirmedWithoutReminderAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        return await dbContext.Appointments
            .Where(a => a.Status == AppointmentStatus.Confirmed)
            .Where(a => a.ReminderSentAt == null)
            .Where(a => a.Start >= windowStart && a.Start <= windowEnd)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
