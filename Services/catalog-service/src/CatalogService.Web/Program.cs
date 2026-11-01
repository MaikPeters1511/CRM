using System.Text;
using CatalogService.Infrastructure;
using CatalogService.Web.ErrorHandling;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults: OpenTelemetry, Health-Checks, Service Discovery, Resilienz-Defaults.
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// doc-agent: Swagger/OpenAPI-Metadaten (Titel, XML-Doc-Kommentare, JWT-Bearer-Scheme fuer die
// [Authorize]-geschuetzten Endpoints, siehe leistungskatalog.openspec.md Abschnitt 5) - reine
// Doku-Konfiguration, keine Aenderung an Controller-Logik.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "CatalogService API",
        Version = "v1",
        Description = "Leistungskatalog (Service Catalog) - siehe UserStories/leistungskatalog.openspec.md.",
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    const string bearerScheme = "Bearer";
    options.AddSecurityDefinition(bearerScheme, new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "JWT Bearer-Token von identity-service (POST /api/auth/login). Rollen: Employee, Admin.",
    });
    var bearerRequirement = new Microsoft.OpenApi.OpenApiSecurityRequirement();
    bearerRequirement.Add(
        new Microsoft.OpenApi.OpenApiSecuritySchemeReference(bearerScheme, null),
        new List<string>());
    options.AddSecurityRequirement(_ => bearerRequirement);
});

builder.Services.AddCatalogServiceInfrastructure(builder.Configuration);

// OWASP A05 (Security Misconfiguration): restriktive CORS-Policy nur fuer den bekannten
// Angular-Origin statt AllowAnyOrigin. Notwendig, da Browser-Requests mit Authorization-Header
// (Defense in Depth, ADR 0001 Abschnitt 4) sonst am CORS-Preflight scheitern, auch wenn das
// Gateway bereits eine eigene Policy hat. Gleiches Muster wie customer-service/employee-service.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(CatalogService.Application.Services.Create.CreateServiceHandler).Assembly);

    // Tests/E2E/README.md, Bug 4: gleiche scoped-DbContext-Service-Location-Ursache wie in den
    // anderen Services (siehe dort fuer die ausfuehrliche Begruendung); gezielter Opt-in statt
    // globaler Lockerung der ServiceLocationPolicy.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<CatalogService.Application.Abstractions.IServiceRepository>();

    var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
        ?? Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
    {
        opts.UseRabbitMq(new Uri(rabbitMqConnectionString)).AutoProvision();

        // Ausgehendes Integration Event fuer ai-service (docs/adr/0003, Folgeaufgaben):
        // haelt dessen Qdrant-Verfuegbarkeitsindex bei Leistungs-Aenderungen aktuell.
        opts.PublishMessage<CatalogService.Application.IntegrationEvents.ServiceCatalogChangedIntegrationEvent>()
            .ToRabbitExchange("service-catalog-changed");
    }
});

// Auth-Validierung unabhaengig vom Gateway (Defense in Depth, ADR 0001 Abschnitt 4):
// catalog-service prueft das JWT eigenstaendig, statt dem Gateway blind zu vertrauen.
// Kein OIDC-Discovery/Authority: schlanker, selbst gehosteter JWT-Issuer (identity-service),
// daher symmetrischer Signing-Key statt Metadaten-Endpoint.
// Konfiguration wird bewusst erst INNERHALB des AddJwtBearer-Options-Delegates gelesen
// (statt eager davor), da dieser Delegate erst lazy beim ersten Request ausgewertet wird -
// zu diesem Zeitpunkt sind auch WebApplicationFactory-Testkonfigurationsueberschreibungen
// (siehe Tests/*.Tests/Web/*WebFactory.cs) bereits in builder.Configuration eingemischt.
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

builder.Services.AddAuthorization();

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
// Gateway-zu-Service-Kommunikation laeuft ueber Aspire Service Discovery per HTTP; ein
// Redirect auf den eigenen HTTPS-Port wuerde diesen Pfad brechen (siehe Tests/E2E/README.md, Bug 3).
app.UseCors("frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
