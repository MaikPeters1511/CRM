// AppHost fuer die CRM-Microservices-Landschaft (siehe docs/adr/0001-microservices-architektur-crm.md).
// Orchestriert: customer-service + eigene PostgreSQL-Instanz (customer_db),
// employee-service + eigene PostgreSQL-Instanz (employee_db), einen gemeinsamen
// RabbitMQ-Broker fuer Wolverine Integration Events (CustomerDeactivated/EmployeeDeactivated)
// sowie das YARP-Gateway als reinen Reverse-Proxy vor beiden Services.
var builder = DistributedApplication.CreateBuilder(args);

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

var customerService = builder.AddProject<Projects.CustomerService_Web>("customer-service")
    .WithReference(customerDb, connectionName: "customer_db")
    .WithReference(rabbitmq, connectionName: "rabbitmq")
    .WaitFor(customerDb)
    .WaitFor(rabbitmq);

var employeeService = builder.AddProject<Projects.EmployeeService_Web>("employee-service")
    .WithReference(employeeDb, connectionName: "employee_db")
    .WithReference(rabbitmq, connectionName: "rabbitmq")
    .WaitFor(employeeDb)
    .WaitFor(rabbitmq);

// API-Gateway (ADR 0001, Abschnitt 4): reiner Reverse-Proxy + Auth-Termination,
// erreicht customer-service/employee-service ausschliesslich ueber Aspire Service Discovery.
builder.AddProject<Projects.Crm_Gateway>("gateway")
    .WithReference(customerService)
    .WithReference(employeeService)
    .WaitFor(customerService)
    .WaitFor(employeeService)
    .WithExternalHttpEndpoints();

builder.Build().Run();
