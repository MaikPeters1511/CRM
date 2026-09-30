using EmployeeService.Application.Abstractions;
using EmployeeService.Infrastructure.Clients;
using EmployeeService.Infrastructure.Persistence;
using EmployeeService.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;

namespace EmployeeService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEmployeeServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("employee_db")
            ?? Environment.GetEnvironmentVariable("EMPLOYEE_DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'employee_db'. Set ConnectionStrings:employee_db or EMPLOYEE_DB_CONNECTION_STRING.");

        services.AddDbContext<EmployeeDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IEmployeeRepository, EfEmployeeRepository>();
        services.AddSingleton(TimeProvider.System);

        AddAppointmentAvailabilityClient(services, configuration);

        // Dev-/Test-only: verknuepft den geseedeten identity-service-Testnutzer mit einem echten
        // Employee-Datensatz (Tests/E2E/README.md, Bug 2). NIEMALS ausserhalb von Development aktiv.
        if (environment.IsDevelopment())
        {
            services.Configure<DevEmployeeSeedOptions>(configuration.GetSection(DevEmployeeSeedOptions.SectionName));
            services.AddHostedService<DevEmployeeSeedHostedService>();
        }

        return services;
    }

    private static void AddAppointmentAvailabilityClient(IServiceCollection services, IConfiguration configuration)
    {
        var appointmentServiceBaseUrl = configuration["Services:AppointmentService:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("APPOINTMENT_SERVICE_BASE_URL")
            ?? "http://appointment-service";

        services.AddHttpClient<IAppointmentAvailabilityClient, AppointmentAvailabilityHttpClient>(client =>
        {
            client.BaseAddress = new Uri(appointmentServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        })
        .AddResilienceHandler("appointment-service-availability", builder =>
        {
            // ADR 0001, Abschnitt 5: kurzer Timeout, max. 2 Retries mit Backoff+Jitter
            // nur für transiente Fehler, Circuit-Breaker, kein Silent-Fallback.
            builder.AddTimeout(TimeSpan.FromSeconds(2));

            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Exception is not null ||
                    (args.Outcome.Result is not null && (int)args.Outcome.Result.StatusCode >= 500)),
            });

            builder.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Exception is not null ||
                    (args.Outcome.Result is not null && (int)args.Outcome.Result.StatusCode >= 500)),
            });
        });
    }
}
