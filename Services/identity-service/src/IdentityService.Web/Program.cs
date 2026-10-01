using System.Threading.RateLimiting;
using IdentityService.Application;
using IdentityService.Infrastructure;
using IdentityService.Web.ErrorHandling;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults: OpenTelemetry, Health-Checks, Service Discovery, Resilienz-Defaults.
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// doc-agent: Swagger/OpenAPI-Metadaten (Titel, XML-Doc-Kommentare) - siehe
// authentifizierung.openspec.md Abschnitt 5. AuthController ist [AllowAnonymous] (Login-Endpoint
// selbst stellt das JWT erst aus), daher kein Bearer-Security-Scheme in diesem Service.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "IdentityService API",
        Version = "v1",
        Description = "Authentifizierung & Identity (schlanker, selbst gehosteter JWT-Issuer) - siehe UserStories/authentifizierung.openspec.md.",
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddIdentityServiceApplication();
builder.Services.AddIdentityServiceInfrastructure(builder.Configuration);

// OWASP A05 (Security Misconfiguration) / A07: restriktive CORS-Policy nur fuer die
// bekannten Angular-Dev-Origins statt AllowAnyOrigin. Ueber Konfiguration erweiterbar,
// damit produktive Frontend-Origins nicht im Code hartkodiert werden muessen.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200", "http://localhost:4201"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// OWASP A04 (Insecure Design) / Brute-Force-Schutz: Login-Endpoint auf wenige
// Versuche pro Zeitfenster und Client-IP begrenzen.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0,
            }));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();

// Kein UseHttpsRedirection() hier: dieser Service ist NICHT direkt von aussen erreichbar
// (siehe docs/adr/0001-microservices-architektur-crm.md, Abschnitt 4 - das Gateway ist der
// einzige oeffentliche HTTPS-Eintrittspunkt, TLS-Termination erfolgt dort). Interne
// Gateway-zu-Service- und Service-zu-Service-Kommunikation laeuft ueber Aspire Service
// Discovery per HTTP; ein Redirect auf den eigenen HTTPS-Port wuerde diesen Pfad brechen
// (siehe Tests/E2E/README.md, Bug 3).
app.UseCors("frontend");
app.UseRateLimiter();

app.MapControllers();

app.Run();

public partial class Program;
