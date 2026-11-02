using System.Text;
using AiService.Infrastructure;
using AiService.Web.ErrorHandling;
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

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "AiService API",
        Version = "v1",
        Description = "KI-Terminassistent (Appointment Assistant) - siehe UserStories/ki-terminassistent.openspec.md.",
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

builder.Services.AddAiServiceInfrastructure(builder.Configuration);

// OWASP A05 (Security Misconfiguration): restriktive CORS-Policy nur fuer den bekannten
// Angular-Origin statt AllowAnyOrigin - identisches Muster wie die anderen Services.
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
    opts.Discovery.IncludeAssembly(
        typeof(AiService.Application.Chat.HandleChatMessage.HandleChatMessageCommand).Assembly);

    // Tests/E2E/README.md, Bug 4 (identisches Muster wie die anderen Services): scoped-DbContext-
    // Service-Location fuer das Repository.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<AiService.Application.Abstractions.IChatSessionRepository>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<AiService.Application.Abstractions.ICatalogQueryClient>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<AiService.Application.Abstractions.IEmployeeQueryClient>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<AiService.Application.Abstractions.IAppointmentQueryClient>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<AiService.Application.Abstractions.ICustomerQueryClient>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<AiService.Application.Abstractions.IAvailabilityIndexClient>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<AiService.Application.Abstractions.ILlmClient>();
    opts.CodeGeneration.AlwaysUseServiceLocationFor<AiService.Application.Abstractions.IAssistantSettingsRepository>();

    var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
        ?? Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
    {
        opts.UseRabbitMq(new Uri(rabbitMqConnectionString)).AutoProvision();

        // DefaultIncomingMessage: die Publisher-Services nutzen eigene CLR-Typnamen (keine geteilte
        // Contracts-Assembly) - ohne festen Zieltyp wuerde Wolverine die Nachricht mangels bekanntem
        // Typnamen verwerfen.
        // Eingehende Integration Events fuer den Qdrant-Verfuegbarkeitsindex (docs/adr/0003, Abschnitt 2).
        opts.ListenToRabbitQueue("ai-service.appointment-created", q => q.BindExchange("appointment-created"))
            .DefaultIncomingMessage<AiService.Application.IndexSync.AppointmentCreatedIntegrationEvent>();
        opts.ListenToRabbitQueue("ai-service.appointment-cancelled", q => q.BindExchange("appointment-cancelled"))
            .DefaultIncomingMessage<AiService.Application.IndexSync.AppointmentCancelledIntegrationEvent>();
        opts.ListenToRabbitQueue("ai-service.appointment-rescheduled", q => q.BindExchange("appointment-rescheduled"))
            .DefaultIncomingMessage<AiService.Application.IndexSync.AppointmentRescheduledIntegrationEvent>();
        opts.ListenToRabbitQueue("ai-service.employee-deactivated", q => q.BindExchange("employee-deactivated"))
            .DefaultIncomingMessage<AiService.Application.IndexSync.EmployeeDeactivatedIntegrationEvent>();
        opts.ListenToRabbitQueue("ai-service.service-catalog-changed", q => q.BindExchange("service-catalog-changed"))
            .DefaultIncomingMessage<AiService.Application.IndexSync.ServiceCatalogChangedIntegrationEvent>();
    }

    // Taeglicher Konsistenz-Sync (ADR 0003, Abschnitt 3) - niedrigpriorer Reparatur-Mechanismus,
    // kein Ersatz fuer die Event-Verarbeitung oben.
    opts.Services.AddHostedService<AiService.Infrastructure.IndexSync.DailyIndexConsistencySyncHostedService>();
});

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

// Kein UseHttpsRedirection(): ai-service ist nicht direkt von aussen erreichbar, TLS-Termination
// erfolgt am Gateway (docs/adr/0001-microservices-architektur-crm.md, Abschnitt 4), identisches
// Muster wie die anderen Services.
app.UseCors("frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
