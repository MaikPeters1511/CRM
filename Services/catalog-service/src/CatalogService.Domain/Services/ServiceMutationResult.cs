namespace CatalogService.Domain.Services;

public sealed class ServiceMutationResult
{
    public Service? Service { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Errors.Count == 0;

    private ServiceMutationResult(Service? service, IReadOnlyList<string> errors)
    {
        Service = service;
        Errors = errors;
    }

    public static ServiceMutationResult Success(Service service) => new(service, []);

    public static ServiceMutationResult Failure(IReadOnlyList<string> errors) => new(null, errors);
}
