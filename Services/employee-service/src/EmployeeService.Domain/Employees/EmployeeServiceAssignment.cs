namespace EmployeeService.Domain.Employees;

/// <summary>
/// Maps a Leistung/Service (owned by a separate bounded context) to an employee.
/// Corresponds to the "EmployeeService" assignment table from the spec; named
/// "EmployeeServiceAssignment" here to avoid a name clash with the service's own
/// root namespace/assembly name "EmployeeService".
/// </summary>
public sealed class EmployeeServiceAssignment
{
    public Guid ServiceId { get; private set; }
    public string ServiceName { get; private set; } = string.Empty;

    private EmployeeServiceAssignment()
    {
    }

    public EmployeeServiceAssignment(Guid serviceId, string serviceName)
    {
        if (serviceId == Guid.Empty)
        {
            throw new ArgumentException("ServiceId must not be empty.", nameof(serviceId));
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("ServiceName must not be empty.", nameof(serviceName));
        }

        ServiceId = serviceId;
        ServiceName = serviceName.Trim();
    }
}
