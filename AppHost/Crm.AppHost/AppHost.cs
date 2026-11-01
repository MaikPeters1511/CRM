// AppHost fuer die CRM-Microservices-Landschaft (siehe docs/adr/0001-microservices-architektur-crm.md).
// Orchestriert: identity-service (JWT-Ausstellung), customer-service + eigene PostgreSQL-Instanz
// (customer_db), employee-service + eigene PostgreSQL-Instanz (employee_db), appointment-service +
// eigene PostgreSQL-Instanz (appointment_db), einen gemeinsamen RabbitMQ-Broker fuer Wolverine
// Integration Events (CustomerDeactivated/EmployeeDeactivated/AppointmentCreated/Cancelled/Rescheduled)
// sowie das YARP-Gateway als reinen Reverse-Proxy vor allen drei fachlichen Services.
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

// Login-Brute-Force-Limit (OWASP A04, siehe identity-service/Program.cs) ist ueber Konfiguration
// einstellbar, damit CI/E2E-Laeufe mit vielen aufeinanderfolgenden Logins das produktive
// 5/Minute-Limit lockern koennen, ohne appsettings.json zu aendern:
//   dotnet user-secrets set "Parameters:login-rate-limit-permit-limit" "1000" --project AppHost/Crm.AppHost
// Bewusst NICHT ueber builder.AddParameter(name, defaultValue) gelesen: dieser Zwei-Argumente-
// Overload liefert nur einen fixen Literal-Wert und wird NICHT durch einen vorhandenen
// "Parameters:<name>"-Konfigurationseintrag (User Secret/Env-Var) ueberschrieben - das gilt
// ebenso fuer jwtIssuer/jwtAudience oben, dort faellt es nur nicht auf, weil sie bisher nie
// overridet werden mussten. Stattdessen direkt aus builder.Configuration lesen (derselbe
// "Parameters:<name>"-Schluessel, aber mit echter Override-Faehigkeit) und als einfachen
// String-Wert durchreichen.
var loginRateLimitPermitLimit = builder.Configuration["Parameters:login-rate-limit-permit-limit"] ?? "5";

// Database-per-Service (ADR 0001, Abschnitt 1 und 7): kein gemeinsamer DB-Server.
// Aspire-Ressourcennamen erlauben keine Unterstriche; die tatsaechliche Connection-String-
// Konfiguration ("ConnectionStrings:customer_db"/"employee_db", siehe *.Infrastructure/DependencyInjection.cs)
// wird ueber den connectionName-Parameter von WithReference erzwungen.
var customerPostgres = builder.AddPostgres("customer-postgres");
var customerDb = customerPostgres.AddDatabase("customer-db", databaseName: "customer_db");

var employeePostgres = builder.AddPostgres("employee-postgres");
var employeeDb = employeePostgres.AddDatabase("employee-db", databaseName: "employee_db");

var appointmentPostgres = builder.AddPostgres("appointment-postgres");
var appointmentDb = appointmentPostgres.AddDatabase("appointment-db", databaseName: "appointment_db");

// catalog-service (leistungskatalog.openspec.md): eigene PostgreSQL-Instanz, kein Konsument
// fuer Integration Events (siehe Proposal "Impact") - daher keine RabbitMQ-Referenz noetig.
var catalogPostgres = builder.AddPostgres("catalog-postgres");
var catalogDb = catalogPostgres.AddDatabase("catalog-db", databaseName: "catalog_db");

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
    .WithEnvironment("IdentitySeed__EmployeePassword", identityEmployeePassword)
    .WithEnvironment("RateLimiting__Login__PermitLimit", loginRateLimitPermitLimit);

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

// appointment-service (terminverwaltung.openspec.md, ADR 0001 Abschnitt 1/2/5/7): benoetigt
// synchrone Service-Discovery-Referenzen auf customer-service/employee-service fuer die
// Polly-abgesicherten Vorbedingungs-Calls (GET /api/customers/{id}, GET /api/employees/{id}/availability).
var appointmentService = builder.AddProject<Projects.AppointmentService_Web>("appointment-service")
    .WithReference(appointmentDb, connectionName: "appointment_db")
    .WithReference(rabbitmq, connectionName: "rabbitmq")
    .WithReference(customerService)
    .WithReference(employeeService)
    .WithEnvironment("Authentication__JwtBearer__Issuer", jwtIssuer)
    .WithEnvironment("Authentication__JwtBearer__Audience", jwtAudience)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", jwtSigningKey)
    .WaitFor(appointmentDb)
    .WaitFor(rabbitmq)
    .WaitFor(customerService)
    .WaitFor(employeeService);

// employee-service ruft umgekehrt appointment-service synchron auf (GET /api/appointments?employeeId=&from=&to=,
// ADR 0001 Abschnitt 2) - daher auch hier eine Service-Discovery-Referenz in dieser Richtung.
employeeService.WithReference(appointmentService);

// catalog-service: nur crm-web ruft diesen Service (ueber das Gateway) auf, kein synchroner
// Backend-zu-Backend-Call von employee-service/appointment-service (kein Gold-Plating, siehe
// Proposal "Impact" und Design-Abschnitt der Change-Spec).
var catalogService = builder.AddProject<Projects.CatalogService_Web>("catalog-service")
    .WithReference(catalogDb, connectionName: "catalog_db")
    .WithEnvironment("Authentication__JwtBearer__Issuer", jwtIssuer)
    .WithEnvironment("Authentication__JwtBearer__Audience", jwtAudience)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", jwtSigningKey)
    .WaitFor(catalogDb);

// API-Gateway (ADR 0001, Abschnitt 4): reiner Reverse-Proxy + Auth-Termination,
// erreicht identity-service/customer-service/employee-service/appointment-service ausschliesslich
// ueber Aspire Service Discovery. Gateway validiert das JWT (Auth-Termination), die Services
// validieren es serverseitig erneut (Defense in Depth) - daher denselben Signing-Key/Issuer/Audience.
var gateway = builder.AddProject<Projects.Crm_Gateway>("gateway")
    .WithReference(identityService)
    .WithReference(customerService)
    .WithReference(employeeService)
    .WithReference(appointmentService)
    .WithReference(catalogService)
    .WithEnvironment("Authentication__JwtBearer__Issuer", jwtIssuer)
    .WithEnvironment("Authentication__JwtBearer__Audience", jwtAudience)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", jwtSigningKey)
    .WaitFor(identityService)
    .WaitFor(customerService)
    .WaitFor(employeeService)
    .WaitFor(appointmentService)
    .WaitFor(catalogService)
    .WithExternalHttpEndpoints();

// crm-web (ADR 0002, Frontend-Konsolidierung): einzige Angular-Anwendung, spricht ausschliesslich
// ueber das Gateway mit allen Services. Fester Dev-Port 4200 (Clients/crm-web/angular.json,
// serve.options.port), konsistent mit der Gateway-CORS-Allowlist (Gateway/Crm.Gateway/appsettings.json).
// isProxied: false, da der Angular Dev-Server selbst direkt an Port 4200 bindet (angular.json) -
// mit aktiviertem DCP-Proxy wuerde Aspire denselben Port 4200 doppelt belegen wollen.
builder.AddJavaScriptApp("crm-web", "../../Clients/crm-web")
    .WithNpm()
    .WithRunScript("start")
    .WithHttpEndpoint(port: 4200, isProxied: false)
    .WithExternalHttpEndpoints()
    .WaitFor(gateway);

builder.Build().Run();
