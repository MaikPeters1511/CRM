using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiService.Application.Abstractions;

namespace AiService.Infrastructure.Qdrant;

/// <summary>
/// Implementiert <see cref="IAvailabilityIndexClient"/> ueber Qdrants HTTP-REST-API (docs/adr/0003,
/// Abschnitt 1/5). Enthaelt ausschliesslich abstrahierte Service-/Mitarbeiter-Embeddings - keine
/// Kunden-/Terminhistorie (ADR Abschnitt 1). Busy/Free-Markierungen (ADR Abschnitt 2, grobe
/// Belegungs-Heuristik) werden bewusst nur als Payload-Zeitstempel gefuehrt, da die harte
/// Verfuegbarkeitsentscheidung ohnehin stets live erfolgt (ADR Abschnitt 4) - kein Gold-Plating
/// durch eine vollstaendige Intervall-Verwaltung in Qdrant selbst.
/// </summary>
public sealed class QdrantAvailabilityIndexClient(HttpClient httpClient) : IAvailabilityIndexClient
{
    private const string ServicesCollection = "ai-service-availability-services";
    private const string EmployeesCollection = "ai-service-availability-employees";

    public async Task UpsertServiceAsync(
        Guid serviceId, string name, string? description, bool isActive, CancellationToken cancellationToken)
    {
        await EnsureCollectionAsync(ServicesCollection, cancellationToken);

        var vector = SimpleTextEmbedding.Embed($"{name} {description}");
        var payload = new Dictionary<string, object?>
        {
            ["serviceId"] = serviceId,
            ["name"] = name,
            ["description"] = description,
            ["isActive"] = isActive,
        };

        await UpsertPointAsync(ServicesCollection, serviceId, vector, payload, cancellationToken);
    }

    public Task RemoveServiceAsync(Guid serviceId, CancellationToken cancellationToken) =>
        DeletePointAsync(ServicesCollection, serviceId, cancellationToken);

    public async Task UpsertEmployeeAsync(
        Guid employeeId, string displayName, IReadOnlyList<Guid> serviceIds, CancellationToken cancellationToken)
    {
        await EnsureCollectionAsync(EmployeesCollection, cancellationToken);

        var vector = SimpleTextEmbedding.Embed(displayName);
        var payload = new Dictionary<string, object?>
        {
            ["employeeId"] = employeeId,
            ["displayName"] = displayName,
            ["serviceIds"] = serviceIds.Select(id => id.ToString()).ToList(),
        };

        await UpsertPointAsync(EmployeesCollection, employeeId, vector, payload, cancellationToken);
    }

    public Task RemoveEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        DeletePointAsync(EmployeesCollection, employeeId, cancellationToken);

    public Task MarkBusyAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        SetPayloadAsync(
            EmployeesCollection, employeeId,
            new Dictionary<string, object?> { ["lastBusyMarkedAt"] = DateTimeOffset.UtcNow, ["lastBusyWindowStart"] = start, ["lastBusyWindowEnd"] = end },
            cancellationToken);

    public Task MarkFreeAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        SetPayloadAsync(
            EmployeesCollection, employeeId,
            new Dictionary<string, object?> { ["lastFreeMarkedAt"] = DateTimeOffset.UtcNow, ["lastFreeWindowStart"] = start, ["lastFreeWindowEnd"] = end },
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> FindCandidateServiceIdsAsync(string queryText, CancellationToken cancellationToken)
    {
        await EnsureCollectionAsync(ServicesCollection, cancellationToken);

        var vector = SimpleTextEmbedding.Embed(queryText);
        var request = new SearchRequest(vector, Limit: 5, ScoreThreshold: 0.2f);

        var response = await httpClient.PostAsJsonAsync($"/collections/{ServicesCollection}/points/search", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: cancellationToken);

        return body?.Result
            .Where(r => r.Payload is not null && r.Payload.TryGetValue("serviceId", out _))
            .Select(r => r.Id)
            .ToList() ?? [];
    }

    private async Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken)
    {
        var exists = await httpClient.GetAsync($"/collections/{collectionName}", cancellationToken);
        if (exists.IsSuccessStatusCode)
        {
            return;
        }

        var createRequest = new CreateCollectionRequest(new VectorParams(SimpleTextEmbedding.VectorSize, "Cosine"));
        var response = await httpClient.PutAsJsonAsync($"/collections/{collectionName}", createRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task UpsertPointAsync(
        string collectionName, Guid pointId, float[] vector, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        var request = new UpsertPointsRequest([new PointInput(pointId, vector, payload)]);
        var response = await httpClient.PutAsJsonAsync($"/collections/{collectionName}/points", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task SetPayloadAsync(
        string collectionName, Guid pointId, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        var request = new SetPayloadRequest(payload, [pointId]);
        var response = await httpClient.PostAsJsonAsync($"/collections/{collectionName}/points/payload", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeletePointAsync(string collectionName, Guid pointId, CancellationToken cancellationToken)
    {
        var request = new DeletePointsRequest([pointId]);
        var response = await httpClient.PostAsJsonAsync($"/collections/{collectionName}/points/delete", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record CreateCollectionRequest([property: JsonPropertyName("vectors")] VectorParams Vectors);

    private sealed record VectorParams(
        [property: JsonPropertyName("size")] int Size, [property: JsonPropertyName("distance")] string Distance);

    private sealed record UpsertPointsRequest([property: JsonPropertyName("points")] IReadOnlyList<PointInput> Points);

    private sealed record PointInput(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("vector")] float[] Vector,
        [property: JsonPropertyName("payload")] Dictionary<string, object?> Payload);

    private sealed record SetPayloadRequest(
        [property: JsonPropertyName("payload")] Dictionary<string, object?> Payload,
        [property: JsonPropertyName("points")] IReadOnlyList<Guid> Points);

    private sealed record DeletePointsRequest([property: JsonPropertyName("points")] IReadOnlyList<Guid> Points);

    private sealed record SearchRequest(
        [property: JsonPropertyName("vector")] float[] Vector,
        [property: JsonPropertyName("limit")] int Limit,
        [property: JsonPropertyName("score_threshold")] float ScoreThreshold,
        [property: JsonPropertyName("with_payload")] bool WithPayload = true);

    private sealed record SearchResponse([property: JsonPropertyName("result")] IReadOnlyList<SearchResultEntry> Result);

    private sealed record SearchResultEntry(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("payload")] Dictionary<string, object>? Payload);
}
