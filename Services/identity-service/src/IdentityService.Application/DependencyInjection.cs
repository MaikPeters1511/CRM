using IdentityService.Application.Auth.Login;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityServiceApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginHandler>();
        return services;
    }
}
