using System.Text;
using System.Text.Json.Serialization;
using AppointmentService.Infrastructure;
using AppointmentService.Web.ErrorHandling;
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
builder.Services.AddSwaggerGen();

builder.Services.AddAppointmentServiceInfrastructure(builder.Configuration);

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(AppointmentService.Application.Appointments.Create.CreateAppointmentCommand).Assembly);

    var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
        ?? Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
    {
        opts.UseRabbitMq(new Uri(rabbitMqConnectionString)).AutoProvision();

        // Ausgehende Integration Events (terminverwaltung.openspec.md, Abschnitt 5/8, ADR 0001 Abschnitt 2/7).
        opts.PublishMessage<AppointmentService.Application.Appointments.IntegrationEvents.AppointmentCreatedIntegrationEvent>()
            .ToRabbitExchange("appointment-created");
        opts.PublishMessage<AppointmentService.Application.Appointments.IntegrationEvents.AppointmentCancelledIntegrationEvent>()
            .ToRabbitExchange("appointment-cancelled");
        opts.PublishMessage<AppointmentService.Application.Appointments.IntegrationEvents.AppointmentRescheduledIntegrationEvent>()
            .ToRabbitExchange("appointment-rescheduled");

        // Eingehendes Integration Event von customer-service (ADR 0001, Abschnitt 3):
        // Kompensationslogik "RequiresReview" bei nachtraeglicher Kundendeaktivierung.
        // customer-service publiziert via `.ToRabbitExchange("customer-deactivated")` (fanout);
        // hier wird eine eigene Queue an diese Exchange gebunden. Kein Wolverine-Durable-Inbox
        // (kein PersistMessagesWithPostgresql im Projekt konfiguriert, analog customer-/employee-
        // service); der Consumer selbst ist bewusst idempotent (CustomerDeactivatedConsumer/
        // Appointment.MarkRequiresReview), daher ausreichend fuer den aktuellen Projektstand.
        opts.ListenToRabbitQueue("appointment-service.customer-deactivated", queue => queue.BindExchange("customer-deactivated"));
    }
});

// Auth-Validierung unabhaengig vom Gateway (Defense in Depth, ADR 0001 Abschnitt 4):
// appointment-service prueft das JWT eigenstaendig, statt dem Gateway blind zu vertrauen.
// Kein OIDC-Discovery/Authority: schlanker, selbst gehosteter JWT-Issuer (identity-service),
// daher symmetrischer Signing-Key statt Metadaten-Endpoint.
// Konfiguration wird bewusst erst INNERHALB des AddJwtBearer-Options-Delegates gelesen
// (statt eager davor), da dieser Delegate erst lazy beim ersten Request ausgewertet wird -
// zu diesem Zeitpunkt sind auch WebApplicationFactory-Testkonfigurationsueberschreibungen
// (siehe Tests/AppointmentService.Tests/Web/*WebFactory.cs) bereits in builder.Configuration eingemischt.
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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
