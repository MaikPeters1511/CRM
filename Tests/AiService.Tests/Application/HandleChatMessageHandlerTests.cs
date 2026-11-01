using AiService.Application.Abstractions;
using AiService.Application.Chat.HandleChatMessage;
using AiService.Tests.Support;

namespace AiService.Tests.Application;

public class HandleChatMessageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero); // Monday.

    private sealed record Fixture(
        FakeChatSessionRepository SessionRepository,
        FakeCatalogQueryClient CatalogClient,
        FakeEmployeeQueryClient EmployeeClient,
        FakeAppointmentQueryClient AppointmentClient,
        FakeCustomerQueryClient CustomerClient,
        FakeAvailabilityIndexClient IndexClient,
        FakeLlmClient LlmClient);

    private static Fixture CreateFixture() => new(
        new FakeChatSessionRepository(), new FakeCatalogQueryClient(), new FakeEmployeeQueryClient(),
        new FakeAppointmentQueryClient(), new FakeCustomerQueryClient(), new FakeAvailabilityIndexClient(),
        new FakeLlmClient());

    private static Task<HandleChatMessageResult> Handle(Fixture fixture, HandleChatMessageCommand command) =>
        HandleChatMessageHandler.Handle(
            command, fixture.SessionRepository, fixture.CatalogClient, fixture.EmployeeClient,
            fixture.AppointmentClient, fixture.CustomerClient, fixture.IndexClient, fixture.LlmClient,
            new FakeTimeProvider(Now), CancellationToken.None);

    private static readonly Guid ColorServiceId = Guid.NewGuid();
    private static readonly Guid EmployeeId = Guid.NewGuid();

    private static void SeedColorServiceWithQualifiedEmployee(Fixture fixture, bool employeeIsQualified = true)
    {
        fixture.CatalogClient.Seed(new ServiceSummaryDto(ColorServiceId, "Farbe", "Haarfarbe", 60));
        fixture.IndexClient.SeedCandidate("farbe", ColorServiceId);
        fixture.EmployeeClient.Seed(new EmployeeSummaryDto(
            EmployeeId, "Anna", "Beispiel", true, employeeIsQualified ? [ColorServiceId] : []));
        fixture.EmployeeClient.SeedAvailability(EmployeeId, new WorkingHoursWindowDto(Now, Now.AddHours(8)));
    }

    // Scenario: Admin fragt ohne Mitarbeiter-Einschränkung
    [Test]
    public async Task Handle_AdminWithRecognizedService_ReturnsSuggestionsAndPersistsBothMessages()
    {
        var fixture = CreateFixture();
        SeedColorServiceWithQualifiedEmployee(fixture);

        var command = new HandleChatMessageCommand(null, "Freier Termin für Farbe", "admin-1", IsAdmin: true, CallerEmployeeId: null);

        var result = await Handle(fixture, command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Suggestions).IsNotEmpty();
        await Assert.That(result.Suggestions[0].ServiceId).IsEqualTo(ColorServiceId);
        await Assert.That(fixture.SessionRepository.Sessions[0].Messages).Count().IsEqualTo(2);
    }

    // Scenario: Anfrage ohne erkennbare Leistung
    [Test]
    public async Task Handle_WithoutRecognizableService_AsksClarifyingQuestionWithoutSuggestions()
    {
        var fixture = CreateFixture();

        var command = new HandleChatMessageCommand(null, "Wann hat Herr Schmidt morgen Zeit?", "employee-1", IsAdmin: false, CallerEmployeeId: EmployeeId);

        var result = await Handle(fixture, command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Suggestions).IsEmpty();
        await Assert.That(result.Reply).Contains("Leistung");
    }

    // Scenario: Employee fragt nach Slot eines anderen Mitarbeiters
    [Test]
    public async Task Handle_EmployeeAsksForAnotherEmployee_ReturnsRestrictedReplyWithoutSuggestions()
    {
        var fixture = CreateFixture();
        SeedColorServiceWithQualifiedEmployee(fixture);

        var command = new HandleChatMessageCommand(
            null, "Wann hat Mitarbeiter B nächste Woche Zeit für einen Haarschnitt?", "employee-1",
            IsAdmin: false, CallerEmployeeId: EmployeeId);

        var result = await Handle(fixture, command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Suggestions).IsEmpty();
        await Assert.That(result.Reply).Contains("eigenen Kalender");
    }

    // Scenario: Vorschlag berücksichtigt Arbeitszeiten und Leistungszuordnung
    [Test]
    public async Task Handle_EmployeeNotQualifiedForService_ReturnsNoSuggestionsAndNoMatchMessage()
    {
        var fixture = CreateFixture();
        SeedColorServiceWithQualifiedEmployee(fixture, employeeIsQualified: false);

        var command = new HandleChatMessageCommand(null, "Freier Termin für Farbe", "employee-1", IsAdmin: false, CallerEmployeeId: EmployeeId);

        var result = await Handle(fixture, command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Suggestions).IsEmpty();
    }

    [Test]
    public async Task Handle_EmployeeAsksForOwnCalendar_ReturnsOwnSuggestionsOnly()
    {
        var fixture = CreateFixture();
        SeedColorServiceWithQualifiedEmployee(fixture);

        var command = new HandleChatMessageCommand(null, "Freier Termin für Farbe", "employee-1", IsAdmin: false, CallerEmployeeId: EmployeeId);

        var result = await Handle(fixture, command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Suggestions).IsNotEmpty();
        var onlyOwnEmployee = result.Suggestions.All(s => s.EmployeeId == EmployeeId);
        await Assert.That(onlyOwnEmployee).IsTrue();
    }

    // Requirement "Qdrant-Index nicht erreichbar"
    [Test]
    public async Task Handle_QdrantUnavailable_ReturnsClearErrorInsteadOfCrashing()
    {
        var fixture = CreateFixture();
        SeedColorServiceWithQualifiedEmployee(fixture);
        fixture.IndexClient.Unavailable = true;

        var command = new HandleChatMessageCommand(null, "Freier Termin für Farbe", "admin-1", IsAdmin: true, CallerEmployeeId: null);

        var result = await Handle(fixture, command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsNotNull();
        await Assert.That(result.Suggestions).IsEmpty();
    }

    // Requirement "Resilienz bei Nichtverfügbarkeit der KI-API"
    [Test]
    public async Task Handle_LlmUnavailable_ReturnsConfiguredFallbackMessageInsteadOfCrashing()
    {
        var fixture = CreateFixture();
        SeedColorServiceWithQualifiedEmployee(fixture);
        fixture.LlmClient.Unavailable = true;

        var command = new HandleChatMessageCommand(null, "Freier Termin für Farbe", "admin-1", IsAdmin: true, CallerEmployeeId: null);

        var result = await Handle(fixture, command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo(
            "Der Assistent ist aktuell nicht verfügbar, bitte nutzen Sie die Kalenderansicht.");
    }

    // Scenario: Freitext-Anfrage mit Kunde, Leistung und Zeitraum (Mehrdeutigkeit bei Kunde)
    [Test]
    public async Task Handle_AmbiguousCustomerName_AsksWhichCustomerInstead()
    {
        var fixture = CreateFixture();
        SeedColorServiceWithQualifiedEmployee(fixture);
        fixture.CustomerClient.Seed(new CustomerSummaryDto(Guid.NewGuid(), "Anna Müller"));
        fixture.CustomerClient.Seed(new CustomerSummaryDto(Guid.NewGuid(), "Peter Müller"));

        var command = new HandleChatMessageCommand(
            null, "Freier Termin für Frau Müller, Farbe", "admin-1", IsAdmin: true, CallerEmployeeId: null);

        var result = await Handle(fixture, command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Suggestions).IsEmpty();
        await Assert.That(result.Reply).Contains("mehrere Kunden");
    }

    // Requirement "Datenminimierung bei externen LLM-Aufrufen"
    [Test]
    public async Task Handle_WithQualifiedEmployee_SendsOnlyMinimizedContextToLlm()
    {
        var fixture = CreateFixture();
        SeedColorServiceWithQualifiedEmployee(fixture);

        var command = new HandleChatMessageCommand(null, "Freier Termin für Farbe", "admin-1", IsAdmin: true, CallerEmployeeId: null);

        await Handle(fixture, command);

        await Assert.That(fixture.LlmClient.LastUserContext).IsNotNull();
        var context = fixture.LlmClient.LastUserContext!;

        // Darf nur abstrakte Zeitfenster/Leistungstyp/Mitarbeiter-Kennung enthalten - keine
        // Kundennamen/-historie oder interne Notizen.
        await Assert.That(context).Contains(EmployeeId.ToString());
        await Assert.That(context).Contains("Farbe");
        await Assert.That(context.Contains("Notiz", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(context.Contains("Historie", StringComparison.OrdinalIgnoreCase)).IsFalse();
    }
}
