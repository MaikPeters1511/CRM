using IdentityService.Application.Audit;
using IdentityService.Application.Auth;
using IdentityService.Domain.Audit;
using IdentityService.Domain.Passkeys;

namespace IdentityService.Application.Passkeys;

public sealed record BeginPasskeyRegistrationCommand(Guid UserId);

public sealed class BeginPasskeyRegistrationHandler(IFido2CeremonyService ceremonyService, IUserRepository userRepository)
{
    public async Task<string> HandleAsync(BeginPasskeyRegistrationCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(command.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Nutzer nicht gefunden.");

        return await ceremonyService.BeginRegistrationAsync(user.Id, user.Email, user.DisplayName, cancellationToken);
    }
}

public sealed record CompletePasskeyRegistrationCommand(Guid UserId, string AttestationResponseJson, string Name, string? IpAddress);

public sealed class CompletePasskeyRegistrationHandler(
    IFido2CeremonyService ceremonyService,
    IPasskeyCredentialRepository repository,
    IAuditLogger auditLogger)
{
    public async Task<PasskeyCredential> HandleAsync(CompletePasskeyRegistrationCommand command, CancellationToken cancellationToken)
    {
        var result = await ceremonyService.CompleteRegistrationAsync(command.UserId, command.AttestationResponseJson, cancellationToken);

        var credential = new PasskeyCredential(
            Guid.NewGuid(), command.UserId, result.CredentialId, result.PublicKey, result.SignCount, command.Name);

        await repository.AddAsync(credential, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditEventType.PasskeyRegistered, command.UserId, command.UserId,
            $"Passkey '{command.Name}' registriert.", command.IpAddress, cancellationToken);

        return credential;
    }
}

public sealed record ListPasskeysQuery(Guid UserId);

public sealed class ListPasskeysHandler(IPasskeyCredentialRepository repository)
{
    public Task<IReadOnlyList<PasskeyCredential>> HandleAsync(ListPasskeysQuery query, CancellationToken cancellationToken) =>
        repository.ListByUserAsync(query.UserId, cancellationToken);
}

public sealed record RemovePasskeyCommand(Guid UserId, Guid PasskeyId, string? IpAddress);

public enum RemovePasskeyStatus
{
    Removed,
    NotFound,
}

public sealed class RemovePasskeyHandler(IPasskeyCredentialRepository repository, IAuditLogger auditLogger)
{
    public async Task<RemovePasskeyStatus> HandleAsync(RemovePasskeyCommand command, CancellationToken cancellationToken)
    {
        var credential = await repository.FindByIdAsync(command.UserId, command.PasskeyId, cancellationToken);
        if (credential is null)
        {
            return RemovePasskeyStatus.NotFound;
        }

        repository.Remove(credential);
        await repository.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditEventType.PasskeyRemoved, command.UserId, command.UserId,
            $"Passkey '{credential.Name}' entfernt.", command.IpAddress, cancellationToken);

        return RemovePasskeyStatus.Removed;
    }
}
