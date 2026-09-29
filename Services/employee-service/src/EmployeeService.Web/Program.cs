using System.Text.Json.Serialization;
using EmployeeService.Infrastructure;
using EmployeeService.Web.ErrorHandling;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddEmployeeServiceInfrastructure(builder.Configuration);

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(EmployeeService.Application.Employees.Create.CreateEmployeeHandler).Assembly);

    var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
        ?? Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
    {
        opts.UseRabbitMq(new Uri(rabbitMqConnectionString)).AutoProvision();
        opts.PublishMessage<EmployeeService.Application.Employees.Deactivate.EmployeeDeactivatedIntegrationEvent>()
            .ToRabbitExchange("employee-deactivated");
    }
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        builder.Configuration.GetSection("Authentication:JwtBearer").Bind(options);
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
