namespace AiService.Application.Chat;

/// <summary>
/// Unverbindlicher Terminvorschlag (ki-terminassistent.openspec.md, Abschnitt 5). Die verbindliche
/// Terminanlage erfolgt ausschliesslich ueber <c>POST /api/appointments</c> in appointment-service
/// (Requirement "Vorschlag ist unverbindlich und erfordert bestaetigte Terminanlage").
/// </summary>
public sealed record AppointmentSuggestionDto(
    Guid EmployeeId,
    string EmployeeName,
    Guid ServiceId,
    string ServiceName,
    Guid? CustomerId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);
