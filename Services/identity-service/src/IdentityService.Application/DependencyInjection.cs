using IdentityService.Application.Audit;
using IdentityService.Application.Auth.ChangePassword;
using IdentityService.Application.Auth.Login;
using IdentityService.Application.Devices;
using IdentityService.Application.Mfa;
using IdentityService.Application.Passkeys;
using IdentityService.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityServiceApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginHandler>();
        services.AddScoped<CompleteMfaLoginHandler>();
        services.AddScoped<ChangePasswordHandler>();

        services.AddScoped<MfaSetupHandler>();
        services.AddScoped<MfaVerifyHandler>();
        services.AddScoped<MfaDisableHandler>();

        services.AddScoped<BeginPasskeyRegistrationHandler>();
        services.AddScoped<CompletePasskeyRegistrationHandler>();
        services.AddScoped<BeginPasskeyLoginHandler>();
        services.AddScoped<CompletePasskeyLoginHandler>();
        services.AddScoped<ListPasskeysHandler>();
        services.AddScoped<RemovePasskeyHandler>();

        services.AddScoped<ConfirmDeviceHandler>();
        services.AddScoped<ListUserDevicesHandler>();
        services.AddScoped<RevokeDeviceHandler>();

        services.AddScoped<GetSecurityPolicyHandler>();
        services.AddScoped<UpdateSecurityPolicyHandler>();

        services.AddScoped<GetAuditLogHandler>();
        services.AddScoped<ExportAuditLogCsvHandler>();

        return services;
    }
}
