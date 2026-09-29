using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

// Auth-Termination gemäß ADR 0001 Abschnitt 4: Das Gateway validiert JWTs zentral.
// Token-Schema (Authority/Audience) ist konsistent zu den Downstream-Services
// (siehe customer-service/employee-service Program.cs, Authentication:JwtBearer).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        builder.Configuration.GetSection("Authentication:JwtBearer").Bind(options);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("gateway-authenticated", policy => policy.RequireAuthenticatedUser());
});

// Reiner Reverse-Proxy: Routing/Cluster-Konfiguration kommt aus appsettings.json
// (pfadbasiert /api/customers/** -> customer-service, /api/employees/** -> employee-service).
// Ziel-Adressen werden über .NET Aspire Service Discovery aufgelöst (WithReference im AppHost),
// keine fachliche Logik/Aggregation im Gateway (ADR 0001 Abschnitt 4).
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();

public partial class Program;
