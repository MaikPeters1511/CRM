using AiService.Application.Abstractions;
using AiService.Infrastructure.Clients;
using AiService.Infrastructure.Llm;
using AiService.Infrastructure.Persistence;
using AiService.Infrastructure.Qdrant;
using AiService.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace AiService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServiceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddTransient<AuthHeaderForwardingHandler>();
        AddServiceAccount(services, configuration);

        AddCrossServiceClients(services, configuration);
        AddQdrantClient(services, configuration);
        AddLlmClient(services, configuration);
        AddAssistantSettings(services);

        return services;
    }

    // ki-einstellungen-backend.openspec.md / docs/adr/0004: Admin-Einstellungen-API. Persistenz in
    // ai_db, API-Key-Maskierung ausschliesslich aus IConfiguration/Environment (niemals Klartext).
    private static void AddAssistantSettings(IServiceCollection services)
    {
        services.AddScoped<IAssistantSettingsRepository, EfAssistantSettingsRepository>();
        services.AddSingleton<IAssistantApiKeyInspector, AssistantApiKeyInspector>();
        services.AddHostedService<AssistantSettingsSeedHostedService>();
    }

    // Technischer Service-Account fuer Hintergrundprozesse (Index-Sync). Zugangsdaten ausschliesslich
    // aus Konfiguration/Secrets (ServiceAccount:Email/Password bzw. SERVICE_ACCOUNT_PASSWORD).
    private static void AddServiceAccount(IServiceCollection services, IConfiguration configuration)
    {
        var options = new ServiceAccountOptions
        {
            Email = configuration["ServiceAccount:Email"] ?? "ai-service@crm.local",
            Password = configuration["ServiceAccount:Password"] ?? Environment.GetEnvironmentVariable("SERVICE_ACCOUNT_PASSWORD"),
        };
        var configuredUrl = configuration["Services:IdentityService:BaseUrl"];
        var identityUrl = !string.IsNullOrWhiteSpace(configuredUrl)
            ? configuredUrl
            : Environment.GetEnvironmentVariable("IDENTITY_SERVICE_BASE_URL") ?? "http://identity-service";

        services.AddHttpClient("identity-service-login", client =>
        {
            client.BaseAddress = new Uri(identityUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        services.AddSingleton<IServiceAccountTokenProvider>(sp =>
            new ServiceAccountTokenProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("identity-service-login"),
                options,
                sp.GetRequiredService<TimeProvider>()));
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ai_db")
            ?? Environment.GetEnvironmentVariable("AI_DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'ai_db'. Set ConnectionStrings:ai_db or AI_DB_CONNECTION_STRING.");

        services.AddDbContext<AiDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IChatSessionRepository, EfChatSessionRepository>();
        services.AddHostedService<DatabaseMigrationHostedService<AiDbContext>>();
    }

    // ADR 0001, Abschnitt 5 (uebernommen fuer ai-service, ki-terminassistent.openspec.md Abschnitt 5):
    // kurzer Timeout, max. 2 Retries mit Exponential Backoff + Jitter, Circuit-Breaker, kein Silent-Fallback.
    private static void AddCrossServiceClients(IServiceCollection services, IConfiguration configuration)
    {
        AddResilientClient<IAppointmentQueryClient, AppointmentServiceHttpClient>(
            services, configuration, "Services:AppointmentService:BaseUrl", "APPOINTMENT_SERVICE_BASE_URL",
            "http://appointment-service", "appointment-service-query");

        AddResilientClient<IEmployeeQueryClient, EmployeeServiceHttpClient>(
            services, configuration, "Services:EmployeeService:BaseUrl", "EMPLOYEE_SERVICE_BASE_URL",
            "http://employee-service", "employee-service-query");

        AddResilientClient<ICatalogQueryClient, CatalogServiceHttpClient>(
            services, configuration, "Services:CatalogService:BaseUrl", "CATALOG_SERVICE_BASE_URL",
            "http://catalog-service", "catalog-service-query");

        AddResilientClient<ICustomerQueryClient, CustomerServiceHttpClient>(
            services, configuration, "Services:CustomerService:BaseUrl", "CUSTOMER_SERVICE_BASE_URL",
            "http://customer-service", "customer-service-query");
    }

    private static void AddResilientClient<TClient, TImplementation>(
        IServiceCollection services,
        IConfiguration configuration,
        string configKey,
        string envVarName,
        string defaultBaseUrl,
        string resilienceHandlerName)
        where TClient : class
        where TImplementation : class, TClient
    {
        var configuredBaseUrl = configuration[configKey];
        var baseUrl = !string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? configuredBaseUrl
            : Environment.GetEnvironmentVariable(envVarName) ?? defaultBaseUrl;

        services.AddHttpClient<TClient, TImplementation>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        })
        .AddHttpMessageHandler<AuthHeaderForwardingHandler>()
        .AddResilienceHandler(resilienceHandlerName, builder =>
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

    private static void AddQdrantClient(IServiceCollection services, IConfiguration configuration)
    {
        var qdrantUrl = AssistantRuntimeConfig.ResolveQdrantBaseUrl(configuration);

        services.AddHttpClient<IAvailabilityIndexClient, QdrantAvailabilityIndexClient>(client =>
        {
            client.BaseAddress = new Uri(qdrantUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        })
        .AddResilienceHandler("qdrant-availability-index", builder =>
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
        });
    }

    // Requirement "Konfigurierbare LLM-Anbindung (OpenAI/LMStudio)": Auswahl ausschliesslich ueber
    // Konfiguration, keine Codeaenderung in HandleChatMessageHandler (kennt nur ILlmClient).
    private static void AddLlmClient(IServiceCollection services, IConfiguration configuration)
    {
        var provider = AssistantRuntimeConfig.ResolveProvider(configuration);

        if (string.Equals(provider, AssistantRuntimeConfig.LmStudioProviderName, StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = configuration["LmStudio:BaseUrl"]
                ?? Environment.GetEnvironmentVariable("LMSTUDIO_BASE_URL")
                ?? "http://localhost:1234";
            var model = AssistantRuntimeConfig.ResolveModel(configuration, provider);

            services.AddHttpClient("lmstudio-llm", client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddResilienceHandler("lmstudio-llm", ConfigureLlmResilience);

            services.AddScoped<ILlmClient>(sp =>
                new LmStudioLlmClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("lmstudio-llm"), model));
        }
        else
        {
            var baseUrl = configuration["OpenAi:BaseUrl"] ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com";
            var model = AssistantRuntimeConfig.ResolveModel(configuration, provider);
            // Keine Secrets im Code (CLAUDE.md): API-Key ausschliesslich ueber Environment
            // Variable/Secret Manager (Aspire-Parameter "openai-api-key", siehe AppHost.cs).
            var apiKey = AssistantRuntimeConfig.ResolveOpenAiApiKey(configuration);

            services.AddHttpClient("openai-llm", client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }
            })
            .AddResilienceHandler("openai-llm", ConfigureLlmResilience);

            services.AddScoped<ILlmClient>(sp =>
                new OpenAiLlmClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai-llm"), model));
        }
    }

    // Requirement "Resilienz bei Nichtverfügbarkeit der KI-API": begrenzte Retries mit Backoff bei
    // Timeout/Rate-Limit (429)/5xx, danach klare Fehlermeldung statt Absturz (siehe ILlmClient).
    private static void ConfigureLlmResilience(Polly.ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder.AddTimeout(TimeSpan.FromSeconds(8));

        builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = 2,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(300),
            ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Exception is not null ||
                (args.Outcome.Result is not null &&
                 ((int)args.Outcome.Result.StatusCode == 429 || (int)args.Outcome.Result.StatusCode >= 500))),
        });
    }
}
