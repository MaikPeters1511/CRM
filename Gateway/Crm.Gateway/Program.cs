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

// OWASP A05: restriktive CORS-Policy nur fuer den bekannten Angular-Dev-Origin statt AllowAnyOrigin.
// Seit der Frontend-Konsolidierung (docs/adr/0002-frontend-konsolidierung.md) existiert nur noch
// ein einziges Frontend (Clients/crm-web) statt drei getrennter Angular-Apps, daher genuegt ein Origin.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Reiner Reverse-Proxy: Routing/Cluster-Konfiguration kommt aus appsettings.json
// (pfadbasiert /api/auth/** -> identity-service (anonym), /api/customers/** -> customer-service,
// /api/employees/** -> employee-service, /api/appointments/** -> appointment-service). Ziel-Adressen
// werden über .NET Aspire Service Discovery aufgelöst (WithReference im AppHost), keine fachliche
// Logik/Aggregation im Gateway (ADR 0001 Abschnitt 4).
// Der validierte Authorization-Header wird von YARP standardmaessig unverändert an die
// Downstream-Services weitergeleitet (kein Token-Neuausstellen im Gateway).
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

builder.Services.AddHttpClient("identity-audit", client =>
{
    client.BaseAddress = new Uri("https+http://identity-service");
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseHttpsRedirection();

app.UseCors("frontend");

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    await next();

    var method = context.Request.Method;
    if (method is not ("POST" or "PUT" or "PATCH" or "DELETE"))
    {
        return;
    }

    if (context.Response.StatusCode is < 200 or >= 300)
    {
        return;
    }

    var path = context.Request.Path.Value ?? string.Empty;
    var auditInfo = MapMutationToAuditEntry(method, path);
    if (auditInfo is null)
    {
        return;
    }

    var authHeader = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
        return;
    }

    try
    {
        var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        using var client = httpClientFactory.CreateClient("identity-audit");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/audit-log");
        requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader);
        requestMessage.Content = JsonContent.Create(new
        {
            action = auditInfo.Value.Action,
            details = auditInfo.Value.Details,
        });
        await client.SendAsync(requestMessage, context.RequestAborted);
    }
    catch
    {
        // Best-effort Audit-Weitergabe im Gateway: darf den eigentlichen Fach-Response niemals abbrechen.
    }
});

app.MapReverseProxy();

app.Run();

static (string Action, string Details)? MapMutationToAuditEntry(string method, string path)
{
    var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (segments.Length < 2 || !segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var resource = segments[1].ToLowerInvariant();
    return resource switch
    {
        "customers" => MapCustomerMutation(method, segments),
        "employees" => MapEmployeeMutation(method, segments),
        "appointments" => MapAppointmentMutation(method, segments),
        "services" => MapServiceMutation(method, segments),
        _ => null,
    };
}

static (string Action, string Details)? MapCustomerMutation(string method, string[] segments)
{
    if (method == "POST" && segments.Length == 2)
        return ("CustomerCreated", "Neuer Kunde angelegt");
    if (method == "PUT" && segments.Length == 3)
        return ("CustomerUpdated", $"Kundendaten aktualisiert (ID: {ShortId(segments[2])})");
    if (method == "POST" && segments.Length == 4 && segments[3].Equals("deactivate", StringComparison.OrdinalIgnoreCase))
        return ("CustomerDeactivated", $"Kunde deaktiviert (ID: {ShortId(segments[2])})");
    return ("DataChanged", $"{method} /api/customers");
}

static (string Action, string Details)? MapEmployeeMutation(string method, string[] segments)
{
    if (method == "POST" && segments.Length == 2)
        return ("EmployeeCreated", "Neuer Mitarbeiter angelegt");
    if (method == "PUT" && segments.Length == 3)
        return ("EmployeeUpdated", $"Mitarbeiter aktualisiert (ID: {ShortId(segments[2])})");
    if (method == "POST" && segments.Length == 4 && segments[3].Equals("deactivate", StringComparison.OrdinalIgnoreCase))
        return ("EmployeeDeactivated", $"Mitarbeiter deaktiviert (ID: {ShortId(segments[2])})");
    if (method == "PUT" && segments.Length == 4 && segments[3].Equals("schedule", StringComparison.OrdinalIgnoreCase))
        return ("WorkingHoursUpdated", $"Arbeitszeiten geändert (Mitarbeiter: {ShortId(segments[2])})");
    if (method == "POST" && segments.Length == 4 && segments[3].Equals("absences", StringComparison.OrdinalIgnoreCase))
        return ("AbsenceCreated", $"Abwesenheit eingetragen (Mitarbeiter: {ShortId(segments[2])})");
    if (method == "DELETE" && segments.Length == 5 && segments[3].Equals("absences", StringComparison.OrdinalIgnoreCase))
        return ("AbsenceDeleted", $"Abwesenheit gelöscht (ID: {ShortId(segments[4])})");
    if (method == "POST" && segments.Length == 6 && segments[3].Equals("absences", StringComparison.OrdinalIgnoreCase))
    {
        var decision = segments[5].ToLowerInvariant();
        if (decision == "approve")
            return ("AbsenceApproved", $"Abwesenheit genehmigt (ID: {ShortId(segments[4])})");
        if (decision == "reject")
            return ("AbsenceRejected", $"Abwesenheit abgelehnt (ID: {ShortId(segments[4])})");
    }
    return ("DataChanged", $"{method} /api/employees");
}

static (string Action, string Details)? MapAppointmentMutation(string method, string[] segments)
{
    if (method == "POST" && segments.Length == 2)
        return ("AppointmentCreated", "Neuer Termin angelegt");
    if ( segments.Length == 4 && segments[3].Equals("reschedule", StringComparison.OrdinalIgnoreCase))
        return ("AppointmentRescheduled", $"Termin verschoben (ID: {ShortId(segments[2])})");
    if (segments.Length == 4 && segments[3].Equals("cancel", StringComparison.OrdinalIgnoreCase))
        return ("AppointmentCancelled", $"Termin storniert (ID: {ShortId(segments[2])})");
    return ("DataChanged", $"{method} /api/appointments");
}

static (string Action, string Details)? MapServiceMutation(string method, string[] segments)
{
    if (method == "POST" && segments.Length == 2)
        return ("ServiceCreated", "Neue Leistung im Katalog angelegt");
    if (method == "PUT" && segments.Length == 3)
        return ("ServiceUpdated", $"Leistung aktualisiert (ID: {ShortId(segments[2])})");
    if ((method == "POST" && segments.Length == 4 && segments[3].Equals("deactivate", StringComparison.OrdinalIgnoreCase))
        || (method == "DELETE" && segments.Length == 3))
        return ("ServiceDeactivated", $"Leistung deaktiviert (ID: {ShortId(segments[2])})");
    return ("DataChanged", $"{method} /api/services");
}

static string ShortId(string id) => id.Length > 8 ? id[..8] : id;

public partial class Program;
