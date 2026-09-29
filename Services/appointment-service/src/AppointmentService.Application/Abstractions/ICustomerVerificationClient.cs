namespace AppointmentService.Application.Abstractions;

/// <summary>
/// Synchronous outbound contract towards <c>customer-service</c> (ADR 0001, Abschnitt 2 &amp; 5):
/// <c>GET /api/customers/{id}</c>, used as a hard precondition check before creating an
/// appointment (existence + active status). Implemented with Polly resilience
/// (see AppointmentService.Infrastructure.DependencyInjection); on dependency failure the
/// implementation SHALL let the exception propagate so the caller can reject the appointment
/// creation with a clear error instead of persisting an inconsistent state (ADR 0001, Abschnitt 5).
/// </summary>
public interface ICustomerVerificationClient
{
    Task<CustomerVerificationResult> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken);
}

public sealed record CustomerVerificationResult(bool Found, bool IsActive)
{
    public static CustomerVerificationResult NotFound() => new(false, false);

    public static CustomerVerificationResult Of(bool isActive) => new(true, isActive);
}
