using System.Text;
using CustomerService.Infrastructure;
using CustomerService.Web.ErrorHandling;
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
builder.Services.AddSwaggerGen();

builder.Services.AddCustomerServiceInfrastructure(builder.Configuration);

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(CustomerService.Application.Customers.Create.CreateCustomerHandler).Assembly);

    var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
        ?? Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
    {
        opts.UseRabbitMq(new Uri(rabbitMqConnectionString)).AutoProvision();
        opts.PublishMessage<CustomerService.Application.Customers.Deactivate.CustomerDeactivatedIntegrationEvent>()
            .ToRabbitExchange("customer-deactivated");
    }
});

// Auth-Validierung unabhaengig vom Gateway (Defense in Depth, ADR 0001 Abschnitt 4):
// customer-service prueft das JWT eigenstaendig, statt dem Gateway blind zu vertrauen.
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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
