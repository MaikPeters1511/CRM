using System.Text.RegularExpressions;

namespace AiService.Application.Chat;

public sealed record ExtractedChatCriteria(
    string? CustomerNameHint,
    IReadOnlyList<string> ServiceTextCandidates,
    string? EmployeeNameHint,
    bool HasDateRangeHint);

/// <summary>
/// Pattern-basierte Entitaetenextraktion aus der Freitext-Chatnachricht (ki-terminassistent.openspec.md,
/// Requirement "Terminvorschlag per Chat-Anfrage"). Bewusst kein eigener LLM-Call fuer diesen Schritt
/// (Design-Abschnitt 7 erlaubt "Pattern-Matching je nach Komplexitaet") - die eigentliche semantische
/// Zuordnung der <see cref="ServiceTextCandidates"/> zu konkreten Katalog-Leistungen (inkl. Synonyme)
/// erfolgt erst nachgelagert ueber den Qdrant-Index (<c>IAvailabilityIndexClient.FindCandidateServiceIdsAsync</c>).
/// </summary>
public static class ChatEntityExtractor
{
    private static readonly Regex CustomerPattern = new(
        @"(?:Frau|Herr|Mrs|Mr|Ms)\s+([A-ZÄÖÜ][\wäöüß\-]+)", RegexOptions.Compiled);

    private static readonly Regex EmployeePattern = new(
        @"Mitarbeiter[a-z]*\s+([A-Za-zÄÖÜäöüß][\wäöüß\-]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] DateRangeWords =
        ["woche", "week", "morgen", "tomorrow", "heute", "today", "montag", "dienstag", "mittwoch",
         "donnerstag", "freitag", "monday", "tuesday", "wednesday", "thursday", "friday"];

    private static readonly char[] ServiceSeparators = ['+', '/'];

    private static readonly string[] ServiceJoinWords = [" und ", " and "];

    private static readonly string[] StopPhraseMarkers =
        ["wann hat", "wann ist", "when does", "when is", "zeit für", "zeit fuer", "time for"];

    public static ExtractedChatCriteria Extract(string message)
    {
        var customerMatch = CustomerPattern.Match(message);
        var employeeMatch = EmployeePattern.Match(message);

        return new ExtractedChatCriteria(
            CustomerNameHint: customerMatch.Success ? customerMatch.Groups[1].Value : null,
            ServiceTextCandidates: ExtractServiceCandidates(message),
            EmployeeNameHint: employeeMatch.Success ? employeeMatch.Groups[1].Value : null,
            HasDateRangeHint: DateRangeWords.Any(w => message.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<string> ExtractServiceCandidates(string message)
    {
        // Nur der Teil nach dem letzten Komma enthaelt typischerweise die Leistungsangabe
        // ("... Frau Müller nächste Woche, Farbe + Schnitt"); ohne Komma wird die gesamte
        // Nachricht betrachtet, aber bekannte Frage-Floskeln ohne Leistungsbezug werden verworfen.
        var lastCommaIndex = message.LastIndexOf(',');
        var clause = lastCommaIndex >= 0 ? message[(lastCommaIndex + 1)..] : message;
        clause = clause.Trim().TrimEnd('?', '.', '!');

        if (lastCommaIndex < 0 &&
            StopPhraseMarkers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        var tokens = clause.Split(ServiceSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var candidates = new List<string>();
        foreach (var token in tokens)
        {
            var parts = SplitOnJoinWords(token);
            candidates.AddRange(parts.Where(p => p.Length >= 3));
        }

        return candidates;
    }

    private static IEnumerable<string> SplitOnJoinWords(string token)
    {
        foreach (var joinWord in ServiceJoinWords)
        {
            if (token.Contains(joinWord, StringComparison.OrdinalIgnoreCase))
            {
                return token.Split(joinWord, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }
        }

        return [token.Trim()];
    }
}
