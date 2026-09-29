using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

// Auth-Termination gemäß ADR 0001 Abschnitt 4: Das Gateway validiert JWTs zentral.
// Token-Schema (Issuer/Audience/SigningKey) ist konsistent zu identity-service (Aussteller) und
// den Downstream-Services (customer-service/employee-service, Authentication:JwtBearer), damit
// diese das vom Gateway bereits validierte Token unabhaengig ERNEUT validieren koennen
// (Defense in Depth - kein reines Vertrauen ins Gateway, siehe ADR 0001).
// Kein OIDC-Discovery/Authority-Metadaten-Endpoint: schlanker, selbst gehosteter JWT-Issuer,
// daher direkte TokenValidationParameters statt Authority-Binding.
// Konfiguration wird bewusst erst INNERHALB des AddJwtBearer-Options-Delegates gelesen
// (statt eager davor), da dieser Delegate erst lazy beim ersten Request ausgewertet wird -
// zu diesem Zeitpunkt sind auch WebApplicationFactory-Testkonfigurationsueberschreibungen bereits
// in builder.Configuration eingemischt (siehe analoges Muster in customer-service/employee-service).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("Authentication:JwtBearer");
        var jwtSigningKey = jwtSection["SigningKey"];

        if (string.IsNullOrWhiteSpace(jwtSigningKey))
        {
            throw new InvalidOperationException(
                "Authentication:JwtBearer:SigningKey ist nicht konfiguriert. Signing-Key muss per " +
                "Environment-Variable/.NET User Secrets gesetzt werden (siehe AppHost.cs, Parameter 'jwt-signing-key').");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("gateway-authenticated", policy => policy.RequireAuthenticatedUser());
});

// OWASP A05: restriktive CORS-Policy nur fuer die bekannten Angular-Dev-Origins statt AllowAnyOrigin.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200", "http://localhost:4201"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Reiner Reverse-Proxy: Routing/Cluster-Konfiguration kommt aus appsettings.json
// (pfadbasiert /api/auth/** -> identity-service (anonym), /api/customers/** -> customer-service,
// /api/employees/** -> employee-service). Ziel-Adressen werden über .NET Aspire Service Discovery
// aufgelöst (WithReference im AppHost), keine fachliche Logik/Aggregation im Gateway (ADR 0001 Abschnitt 4).
// Der validierte Authorization-Header wird von YARP standardmaessig unverändert an die
// Downstream-Services weitergeleitet (kein Token-Neuausstellen im Gateway).
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseHttpsRedirection();

app.UseCors("frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();

public partial class Program;
