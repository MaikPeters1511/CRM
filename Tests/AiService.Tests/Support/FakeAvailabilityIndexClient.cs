using AiService.Application.Abstractions;

namespace AiService.Tests.Support;

public sealed class FakeAvailabilityIndexClient : IAvailabilityIndexClient
{
    private readonly Dictionary<string, List<Guid>> _candidatesByText = [];

    public bool Unavailable { get; set; }

    public List<Guid> UpsertedServiceIds { get; } = [];
    public List<Guid> RemovedServiceIds { get; } = [];
    public List<Guid> UpsertedEmployeeIds { get; } = [];
    public List<Guid> RemovedEmployeeIds { get; } = [];
    public List<Guid> MarkedBusyEmployeeIds { get; } = [];

    public void SeedCandidate(string textFragment, Guid serviceId)
    {
        if (!_candidatesByText.TryGetValue(textFragment, out var ids))
        {
            ids = [];
            _candidatesByText[textFragment] = ids;
        }

        ids.Add(serviceId);
    }

    public Task UpsertServiceAsync(Guid serviceId, string name, string? description, bool isActive, CancellationToken cancellationToken)
    {
        UpsertedServiceIds.Add(serviceId);
        return Task.CompletedTask;
    }

    public Task RemoveServiceAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        RemovedServiceIds.Add(serviceId);
        return Task.CompletedTask;
    }

    public Task UpsertEmployeeAsync(Guid employeeId, string displayName, IReadOnlyList<Guid> serviceIds, CancellationToken cancellationToken)
    {
        UpsertedEmployeeIds.Add(employeeId);
        return Task.CompletedTask;
    }

    public Task RemoveEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        RemovedEmployeeIds.Add(employeeId);
        return Task.CompletedTask;
    }

    public Task MarkBusyAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        MarkedBusyEmployeeIds.Add(employeeId);
        return Task.CompletedTask;
    }

    public Task MarkFreeAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<Guid>> FindCandidateServiceIdsAsync(string queryText, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            throw new AvailabilityIndexUnavailableException("Qdrant nicht erreichbar (Test).");
        }

        foreach (var (fragment, ids) in _candidatesByText)
        {
            if (queryText.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<Guid>>(ids);
            }
        }

        return Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
