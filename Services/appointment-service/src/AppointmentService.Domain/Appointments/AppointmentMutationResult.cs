namespace AppointmentService.Domain.Appointments;

public enum AppointmentMutationFailureReason
{
    None,
    Validation,
    Conflict,
}

public sealed record AppointmentMutationResult(
    bool IsSuccess,
    Appointment? Appointment,
    IReadOnlyList<string> Errors,
    AppointmentMutationFailureReason Reason)
{
    public static AppointmentMutationResult Success(Appointment appointment) =>
        new(true, appointment, [], AppointmentMutationFailureReason.None);

    public static AppointmentMutationResult Failure(IReadOnlyList<string> errors, AppointmentMutationFailureReason reason) =>
        new(false, null, errors, reason);
}
