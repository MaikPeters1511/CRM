using AppointmentService.Application.Abstractions;
using AppointmentService.Infrastructure.BackgroundJobs;
using AppointmentService.Infrastructure.Clients;
using AppointmentService.Infrastructure.Notifications;
using AppointmentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace AppointmentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAppointmentServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("appointment_db")
            ?? Environment.GetEnvironmentVariable("APPOINTMENT_DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'appointment_db'. Set ConnectionStrings:appointment_db or APPOINTMENT_DB_CONNECTION_STRING.");

        services.AddDbContext<AppointmentDbContext>(options => options.UseNpgsql(connectionString));

        // Tests/E2E/README.md, Bug 5: wendet beim Start automatisch alle ausstehenden Migrationen
        // an (jede Umgebung, nicht nur Development - kein separater Migrations-Deployment-Schritt
        // in dieser Landschaft vorhanden).
        services.AddHostedService<DatabaseMigrationHostedService<AppointmentDbContext>>();

        services.AddScoped<IAppointmentRepository, EfAppointmentRepository>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IReminderNotifier, LogReminderNotifier>();
        services.AddHostedService<AppointmentReminderHostedService>();

        AddCustomerVerificationClient(services, configuration);
        AddEmployeeAvailabilityClient(services, configuration);

        return services;
    }

    // ADR 0001, Abschnitt 5: kurzer Timeout (2-3s), max. 2 Retries mit Exponential Backoff +
    // Jitter (nur transiente Fehler, keine 4xx), Circuit-Breaker, kein Silent-Fallback.
    // Identisches Muster wie EmployeeService.Infrastructure.DependencyInjection.

    private static void AddCustomerVerificationClient(IServiceCollection services, IConfiguration configuration)
    {
        var customerServiceBaseUrl = configuration["Services:CustomerService:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("CUSTOMER_SERVICE_BASE_URL")
            ?? "http://customer-service";

        services.AddHttpClient<ICustomerVerificationClient, CustomerVerificationHttpClient>(client =>
        {
            client.BaseAddress = new Uri(customerServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        })
        .AddResilienceHandler("customer-service-verification", builder =>
        {
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

    private static void AddEmployeeAvailabilityClient(IServiceCollection services, IConfiguration configuration)
    {
        var employeeServiceBaseUrl = configuration["Services:EmployeeService:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("EMPLOYEE_SERVICE_BASE_URL")
            ?? "http://employee-service";

        services.AddHttpClient<IEmployeeAvailabilityClient, EmployeeAvailabilityHttpClient>(client =>
        {
            client.BaseAddress = new Uri(employeeServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        })
        .AddResilienceHandler("employee-service-availability", builder =>
        {
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
