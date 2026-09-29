namespace EmployeeService.Application.Employees.Availability;

public sealed record GetEmployeeAvailabilityQuery(Guid EmployeeId, DateOnly From, DateOnly To);

public sealed record AvailableSlotDto(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);

public sealed record GetEmployeeAvailabilityResult(bool Found, IReadOnlyList<AvailableSlotDto> Slots)
{
    public static GetEmployeeAvailabilityResult NotFound() => new(false, []);

    public static GetEmployeeAvailabilityResult Success(IReadOnlyList<AvailableSlotDto> slots) => new(true, slots);
}
