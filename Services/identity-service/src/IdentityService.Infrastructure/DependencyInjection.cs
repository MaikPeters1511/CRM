using IdentityService.Application.Auth;
using IdentityService.Infrastructure.Auth;
using IdentityService.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<IdentitySeedOptions>(configuration.GetSection(IdentitySeedOptions.SectionName));

        services.AddSingleton<IUserRepository, InMemorySeedUserRepository>();
        services.AddSingleton<IPasswordVerifier, IdentityPasswordVerifier>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
