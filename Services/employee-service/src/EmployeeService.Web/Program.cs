using System.Text;
using System.Text.Json.Serialization;
using EmployeeService.Infrastructure;
using EmployeeService.Web.ErrorHandling;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults: OpenTelemetry, Health-Checks, Service Discovery, Resilienz-Defaults.
builder.AddServiceDefaults();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// doc-agent: Swagger/OpenAPI-Metadaten (Titel, XML-Doc-Kommentare, JWT-Bearer-Scheme fuer die
// [Authorize]-geschuetzten Endpoints, siehe mitarbeiterverwaltung.openspec.md Abschnitt 5) -
// reine Doku-Konfiguration, keine Aenderung an Controller-Logik.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "EmployeeService API",
        Version = "v1",
        Description = "Mitarbeiterverwaltung (Employee Management) - siehe UserStories/mitarbeiterverwaltung.openspec.md.",
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
        Description = "JWT Bearer-Token von identity-service (POST /api/auth/login). Rollen: Employee, Admin (Ownership-Check bei Abwesenheiten).",
    });
    var bearerRequirement = new Microsoft.OpenApi.OpenApiSecurityRequirement();
    bearerRequirement.Add(
        new Microsoft.OpenApi.OpenApiSecuritySchemeReference(bearerScheme, null),
        new List<string>());
    options.AddSecurityRequirement(_ => bearerRequirement);
});

builder.Services.AddEmployeeServiceInfrastructure(builder.Configuration, builder.Environment);

// OWASP A05 (Security Misconfiguration): restriktive CORS-Policy nur fuer den bekannten
// Angular-Origin statt AllowAnyOrigin. Notwendig, da Browser-Requests mit Authorization-Header
// (Defense in Depth, ADR 0001 Abschnitt 4) sonst am CORS-Preflight scheitern, auch wenn das
// Gateway bereits eine eigene Policy hat. Gleiches Muster wie identity-service/Gateway.
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
    opts.Discovery.IncludeAssembly(typeof(EmployeeService.Application.Employees.Create.CreateEmployeeHandler).Assembly);

    // Tests/E2E/README.md, Bug 4: EF Core registriert DbContextOptions<EmployeeDbContext>
    // ueber AddDbContext als scoped "opaque lambda factory" (EntityFrameworkServiceCollectionExtensions.
    // CreateDbContextOptions). Wolverines Codegen kann daraus per Konstruktor-Injection keinen Aufrufcode
    // erzeugen und wirft mit ServiceLocationPolicy.NotAllowed (Default seit Wolverine 6.0) eine
    // InvalidServiceLocationException fuer jeden Handler, der (transitiv ueber IEmployeeRepository ->
    // EfEmployeeRepository -> EmployeeDbContext) davon abhaengt. Fix laut Wolverine-Doku
    // (https://wolverinefx.net/guide/codegen.html): den einzigen Service, dessen Registrierung das
    // erfordert, gezielt fuer Service Location opt-in, statt die Policy global zu lockern.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<EmployeeService.Application.Abstractions.IEmployeeRepository>();

    // Tests/E2E/README.md, Bug 7: derselbe "opaque lambda factory"-Registrierungsmechanismus
    // (hier: AddHttpClient<TClient, TImplementation>, Transient statt Scoped, aber identisches
    // Grundproblem fuer Wolverines Codegen) trifft auch auf IAppointmentAvailabilityClient zu
    // (AddAbsenceHandler). Der Bug-4-Fix oben deckte nur IEmployeeRepository ab.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<EmployeeService.Application.Abstractions.IAppointmentAvailabilityClient>();

    // Gleiche Begruendung wie bei IEmployeeRepository oben: INotificationRepository ist (wie
    // IEmployeeRepository) ueber EfNotificationRepository(EmployeeDbContext) an die per
    // AddDbContext opaque-lambda-registrierte DbContextOptions<EmployeeDbContext> gekoppelt.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<EmployeeService.Application.Abstractions.INotificationRepository>();

    var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
        ?? Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
    {
        opts.UseRabbitMq(new Uri(rabbitMqConnectionString)).AutoProvision();
        opts.PublishMessage<EmployeeService.Application.Employees.Deactivate.EmployeeDeactivatedIntegrationEvent>()
            .ToRabbitExchange("employee-deactivated");
        opts.PublishMessage<EmployeeService.Application.Employees.ApproveAbsence.AbsenceApprovedIntegrationEvent>()
            .ToRabbitExchange("absence-approved");
        opts.PublishMessage<EmployeeService.Application.Employees.RejectAbsence.AbsenceRejectedIntegrationEvent>()
            .ToRabbitExchange("absence-rejected");
    }
});

// Auth-Validierung unabhaengig vom Gateway (Defense in Depth, ADR 0001 Abschnitt 4):
// employee-service prueft das JWT eigenstaendig, statt dem Gateway blind zu vertrauen.
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
// Gateway-zu-Service- und Service-zu-Service-Kommunikation laeuft ueber Aspire Service
// Discovery per HTTP; ein Redirect auf den eigenen HTTPS-Port wuerde diesen Pfad brechen
// (siehe Tests/E2E/README.md, Bug 3).
app.UseCors("frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
