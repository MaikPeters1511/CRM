// AppHost fuer die CRM-Microservices-Landschaft (siehe docs/adr/0001-microservices-architektur-crm.md).
// Orchestriert: identity-service (JWT-Ausstellung), customer-service + eigene PostgreSQL-Instanz
// (customer_db), employee-service + eigene PostgreSQL-Instanz (employee_db), einen gemeinsamen
// RabbitMQ-Broker fuer Wolverine Integration Events (CustomerDeactivated/EmployeeDeactivated)
// sowie das YARP-Gateway als reinen Reverse-Proxy vor allen drei Services.
var builder = DistributedApplication.CreateBuilder(args);

// Gemeinsames JWT-Schema (Issuer/Audience/SigningKey) fuer identity-service (Aussteller) und
// Gateway/customer-service/employee-service (Validierer, Defense in Depth gemaess ADR 0001).
// SigningKey ist ein Aspire-Secret-Parameter: MUSS lokal per .NET User Secrets gesetzt werden,
// niemals im Code/appsettings hardcoden:
//   dotnet user-secrets set "Parameters:jwt-signing-key" "<mind. 32 zufaellige Byte, z.B. via `openssl rand -base64 32`>" --project AppHost/Crm.AppHost
// Optional (Dev-Bequemlichkeit, gleiches Prinzip): Parameters:identity-admin-password / identity-employee-password.
var jwtIssuer = builder.AddParameter("jwt-issuer", "https://crm-identity");
var jwtAudience = builder.AddParameter("jwt-audience", "crm-api");
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);
var identityAdminPassword = builder.AddParameter("identity-admin-password", secret: true);
var identityEmployeePassword = builder.AddParameter("identity-employee-password", secret: true);

// Database-per-Service (ADR 0001, Abschnitt 1 und 7): kein gemeinsamer DB-Server.
// Aspire-Ressourcennamen erlauben keine Unterstriche; die tatsaechliche Connection-String-
// Konfiguration ("ConnectionStrings:customer_db"/"employee_db", siehe *.Infrastructure/DependencyInjection.cs)
// wird ueber den connectionName-Parameter von WithReference erzwungen.
var customerPostgres = builder.AddPostgres("customer-postgres");
var customerDb = customerPostgres.AddDatabase("customer-db", databaseName: "customer_db");

var employeePostgres = builder.AddPostgres("employee-postgres");
var employeeDb = employeePostgres.AddDatabase("employee-db", databaseName: "employee_db");

// Gemeinsamer Message-Broker fuer Wolverine Integration Events (ADR 0001, Abschnitt 2/7).
var rabbitmq = builder.AddRabbitMQ("rabbitmq");

// identity-service: eigenstaendiger, selbst gehosteter JWT-Issuer (kein externer IdP,
// siehe UserStories/authentifizierung.openspec.md). Kein identity_db in diesem Entwicklungsstand:
// nur zwei Seed-Testnutzer (Admin/Employee), in-memory gehasht ueber PasswordHasher<T>
// (siehe InMemorySeedUserRepository) - bewusste Vereinfachung, kein Gold-Plating.
var identityService = builder.AddProject<Projects.IdentityService_Web>("identity-service")
    .WithEnvironment("Authentication__JwtBearer__Issuer", jwtIssuer)
    .WithEnvironment("Authentication__JwtBearer__Audience", jwtAudience)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", jwtSigningKey)
    .WithEnvironment("IdentitySeed__AdminPassword", identityAdminPassword)
    .WithEnvironment("IdentitySeed__EmployeePassword", identityEmployeePassword);

var customerService = builder.AddProject<Projects.CustomerService_Web>("customer-service")
    .WithReference(customerDb, connectionName: "customer_db")
    .WithReference(rabbitmq, connectionName: "rabbitmq")
    .WithEnvironment("Authentication__JwtBearer__Issuer", jwtIssuer)
    .WithEnvironment("Authentication__JwtBearer__Audience", jwtAudience)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", jwtSigningKey)
    .WaitFor(customerDb)
    .WaitFor(rabbitmq);

var employeeService = builder.AddProject<Projects.EmployeeService_Web>("employee-service")
    .WithReference(employeeDb, connectionName: "employee_db")
    .WithReference(rabbitmq, connectionName: "rabbitmq")
    .WithEnvironment("Authentication__JwtBearer__Issuer", jwtIssuer)
    .WithEnvironment("Authentication__JwtBearer__Audience", jwtAudience)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", jwtSigningKey)
    .WaitFor(employeeDb)
    .WaitFor(rabbitmq);

// API-Gateway (ADR 0001, Abschnitt 4): reiner Reverse-Proxy + Auth-Termination,
// erreicht identity-service/customer-service/employee-service ausschliesslich ueber
// Aspire Service Discovery. Gateway validiert das JWT (Auth-Termination), die Services
// validieren es serverseitig erneut (Defense in Depth) - daher denselben Signing-Key/Issuer/Audience.
builder.AddProject<Projects.Crm_Gateway>("gateway")
    .WithReference(identityService)
    .WithReference(customerService)
    .WithReference(employeeService)
    .WithEnvironment("Authentication__JwtBearer__Issuer", jwtIssuer)
    .WithEnvironment("Authentication__JwtBearer__Audience", jwtAudience)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", jwtSigningKey)
    .WaitFor(identityService)
    .WaitFor(customerService)
    .WaitFor(employeeService)
    .WithExternalHttpEndpoints();

builder.Build().Run();
