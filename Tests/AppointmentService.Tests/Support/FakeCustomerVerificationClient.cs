using AppointmentService.Application.Abstractions;

namespace AppointmentService.Tests.Support;

public sealed class FakeCustomerVerificationClient : ICustomerVerificationClient
{
    private readonly Dictionary<Guid, bool> _activeById = [];
    public bool ThrowOnCall { get; set; }

    public void SeedActive(Guid customerId, bool isActive = true) => _activeById[customerId] = isActive;

    public Task<CustomerVerificationResult> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (ThrowOnCall)
        {
            throw new HttpRequestException("customer-service unavailable (test).");
        }

        if (!_activeById.TryGetValue(customerId, out var isActive))
        {
            return Task.FromResult(CustomerVerificationResult.NotFound());
        }

        return Task.FromResult(CustomerVerificationResult.Of(isActive));
    }
}
