using AiService.Application.Chat;

namespace AiService.Tests.Application;

public class ChatEntityExtractorTests
{
    // Scenario: Freitext-Anfrage mit Kunde, Leistung und Zeitraum
    [Test]
    public async Task Extract_WithCustomerServiceAndDateRange_RecognizesAllEntities()
    {
        var criteria = ChatEntityExtractor.Extract("Freier Termin für Frau Müller nächste Woche, Farbe + Schnitt");

        await Assert.That(criteria.CustomerNameHint).IsEqualTo("Müller");
        await Assert.That(criteria.ServiceTextCandidates).Contains("Farbe");
        await Assert.That(criteria.ServiceTextCandidates).Contains("Schnitt");
        await Assert.That(criteria.HasDateRangeHint).IsTrue();
    }

    // Scenario: Anfrage ohne erkennbare Leistung
    [Test]
    public async Task Extract_WithoutServiceHint_ReturnsNoServiceCandidates()
    {
        var criteria = ChatEntityExtractor.Extract("Wann hat Herr Schmidt morgen Zeit?");

        await Assert.That(criteria.ServiceTextCandidates).IsEmpty();
    }

    [Test]
    public async Task Extract_WithEmployeeMention_RecognizesEmployeeNameHint()
    {
        var criteria = ChatEntityExtractor.Extract("Wann hat Mitarbeiter B nächste Woche Zeit für einen Haarschnitt?");

        await Assert.That(criteria.EmployeeNameHint).IsEqualTo("B");
    }
}
