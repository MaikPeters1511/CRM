using AiService.Application.Abstractions;
using AiService.Domain.Chat;

namespace AiService.Application.Chat.HandleChatMessage;

/// <summary>
/// Orchestriert den Ablauf aus ki-terminassistent.openspec.md Abschnitt 5/7: Entitaetenextraktion
/// -&gt; Qdrant-Kandidaten-Vorauswahl -&gt; Polly-abgesicherte Live-Calls an
/// appointment-service/employee-service/catalog-service/customer-service -&gt; RBAC-Filter nach
/// <c>EmployeeId</c>-Claim -&gt; LLM-Call zur Antwortformulierung. Der Assistent legt, aendert oder
/// storniert selbst NIEMALS einen Termin (Requirement "Vorschlag ist unverbindlich").
/// </summary>
public static class HandleChatMessageHandler
{
    private const string AssistantUnavailableMessage =
        "Der Assistent ist aktuell nicht verfügbar, bitte nutzen Sie die Kalenderansicht.";

    private const string NoServiceRecognizedMessage =
        "Für welche Leistung möchten Sie einen Termin? Bitte nennen Sie mir die gewünschte Leistung.";

    private const string EmployeeRestrictedToOwnCalendarMessage =
        "Ich kann nur Vorschläge für Ihren eigenen Kalender machen.";

    public static async Task<HandleChatMessageResult> Handle(
        HandleChatMessageCommand command,
        IChatSessionRepository sessionRepository,
        ICatalogQueryClient catalogClient,
        IEmployeeQueryClient employeeClient,
        IAppointmentQueryClient appointmentClient,
        ICustomerQueryClient customerClient,
        IAvailabilityIndexClient indexClient,
        ILlmClient llmClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await ResolveSessionAsync(command, sessionRepository, now, cancellationToken);
        if (session is null)
        {
            return HandleChatMessageResult.Forbidden();
        }

        session.AddMessage(ChatRole.User, command.Message, now);

        var criteria = ChatEntityExtractor.Extract(command.Message);

        // RBAC (Requirement "RBAC-konforme Sichtbarkeit von Terminvorschlägen"): serverseitig anhand
        // der JWT-Claims erzwungen, NICHT anhand des Chat-Texts - Nennen eines fremden Mitarbeiters
        // im Chat darf die eigene Kalender-Einschraenkung nicht umgehen.
        if (!command.IsAdmin && criteria.EmployeeNameHint is not null)
        {
            return await RespondAndPersist(
                session, sessionRepository, EmployeeRestrictedToOwnCalendarMessage, [], true, null, now, cancellationToken);
        }

        if (criteria.ServiceTextCandidates.Count == 0)
        {
            return await RespondAndPersist(
                session, sessionRepository, NoServiceRecognizedMessage, [], true, null, now, cancellationToken);
        }

        IReadOnlyList<Guid> candidateServiceIds;
        try
        {
            var resolved = new HashSet<Guid>();
            foreach (var candidate in criteria.ServiceTextCandidates)
            {
                foreach (var serviceId in await indexClient.FindCandidateServiceIdsAsync(candidate, cancellationToken))
                {
                    resolved.Add(serviceId);
                }
            }

            candidateServiceIds = resolved.ToList();
        }
        catch (AvailabilityIndexUnavailableException)
        {
            // Requirement "Resilienz bei Nichtverfügbarkeit der KI-API", Scenario "Qdrant-Index nicht
            // erreichbar": klare Fehlermeldung statt (moeglicherweise veralteter) Vorschlaege.
            return HandleChatMessageResult.Failure(session.Id, AssistantUnavailableMessage);
        }

        if (candidateServiceIds.Count == 0)
        {
            return await RespondAndPersist(
                session, sessionRepository, NoServiceRecognizedMessage, [], true, null, now, cancellationToken);
        }

        Guid? resolvedCustomerId = null;
        if (criteria.CustomerNameHint is not null)
        {
            var customers = await customerClient.SearchByNameAsync(criteria.CustomerNameHint, cancellationToken);
            if (customers.Count > 1)
            {
                var names = string.Join(", ", customers.Select(c => c.FullName));
                return await RespondAndPersist(
                    session, sessionRepository,
                    $"Es gibt mehrere Kunden mit diesem Namen ({names}). Welchen meinen Sie?",
                    [], true, null, now, cancellationToken);
            }

            resolvedCustomerId = customers.Count == 1 ? customers[0].CustomerId : null;
        }

        var from = DateOnly.FromDateTime(now.Date);
        var to = criteria.HasDateRangeHint ? from.AddDays(7) : from.AddDays(1);

        IReadOnlyList<EmployeeSummaryDto> targetEmployees;
        try
        {
            targetEmployees = await ResolveTargetEmployeesAsync(
                command, candidateServiceIds, employeeClient, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HandleChatMessageResult.Failure(session.Id, AssistantUnavailableMessage);
        }

        if (targetEmployees.Count == 0)
        {
            return await RespondAndPersist(
                session, sessionRepository,
                "Für diese Leistung ist aktuell kein qualifizierter Mitarbeiter verfügbar.",
                [], true, null, now, cancellationToken);
        }

        var services = await catalogClient.SearchActiveServicesAsync(string.Empty, cancellationToken);
        var relevantServices = services.Where(s => candidateServiceIds.Contains(s.ServiceId)).ToList();

        var suggestions = new List<AppointmentSuggestionDto>();
        try
        {
            foreach (var employee in targetEmployees)
            {
                var qualifiedServiceIds = employee.ServiceIds.ToHashSet();
                var employeeRelevantServices = relevantServices.Where(s => qualifiedServiceIds.Contains(s.ServiceId)).ToList();
                if (employeeRelevantServices.Count == 0)
                {
                    continue;
                }

                var workingWindows = await employeeClient.GetAvailabilityAsync(employee.EmployeeId, from, to, cancellationToken);
                var bookedSlots = await appointmentClient.GetBookedSlotsAsync(employee.EmployeeId, from, to, cancellationToken);

                foreach (var service in employeeRelevantServices)
                {
                    var freeWindows = FreeSlotCalculator.ComputeFreeWindows(
                        workingWindows, bookedSlots, service.DefaultDurationMinutes ?? 60);

                    foreach (var window in freeWindows.Take(3))
                    {
                        suggestions.Add(new AppointmentSuggestionDto(
                            employee.EmployeeId,
                            $"{employee.FirstName} {employee.LastName}",
                            service.ServiceId,
                            service.Name,
                            resolvedCustomerId,
                            window.Start,
                            window.End));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HandleChatMessageResult.Failure(session.Id, AssistantUnavailableMessage);
        }

        if (suggestions.Count == 0)
        {
            return await RespondAndPersist(
                session, sessionRepository,
                "Für die angefragte Leistung und den Mitarbeiter gibt es im gewählten Zeitraum keinen freien Termin.",
                [], true, null, now, cancellationToken);
        }

        string reply;
        try
        {
            reply = await llmClient.CompleteAsync(BuildSystemPrompt(), BuildMinimizedUserContext(suggestions), cancellationToken);
        }
        catch (LlmUnavailableException)
        {
            // Requirement "Resilienz bei Nichtverfügbarkeit der KI-API": Polly-Retries sind bereits in
            // der Infrastructure-Schicht (HttpClient-Resilience-Handler) ausgeschoepft worden.
            return HandleChatMessageResult.Failure(session.Id, AssistantUnavailableMessage);
        }

        return await RespondAndPersist(session, sessionRepository, reply, suggestions, true, null, now, cancellationToken);
    }

    private static async Task<ChatSession?> ResolveSessionAsync(
        HandleChatMessageCommand command,
        IChatSessionRepository repository,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (command.SessionId is { } sessionId)
        {
            var existing = await repository.GetByIdAsync(sessionId, cancellationToken);
            if (existing is not null)
            {
                return existing.IsOwnedBy(command.CallerUserId) ? existing : null;
            }
        }

        var session = ChatSession.Start(command.CallerUserId, now);
        await repository.AddAsync(session, cancellationToken);
        return session;
    }

    private static async Task<IReadOnlyList<EmployeeSummaryDto>> ResolveTargetEmployeesAsync(
        HandleChatMessageCommand command,
        IReadOnlyList<Guid> candidateServiceIds,
        IEmployeeQueryClient employeeClient,
        CancellationToken cancellationToken)
    {
        if (!command.IsAdmin)
        {
            // Requirement "RBAC-konforme Sichtbarkeit von Terminvorschlägen": Employee erhaelt
            // ausschliesslich Vorschlaege fuer die eigene EmployeeId, unabhaengig vom Chat-Text.
            if (command.CallerEmployeeId is not { } ownEmployeeId)
            {
                return [];
            }

            var own = await employeeClient.GetByIdAsync(ownEmployeeId, cancellationToken);
            return own is { IsActive: true } ? [own] : [];
        }

        var qualified = new Dictionary<Guid, EmployeeSummaryDto>();
        foreach (var serviceId in candidateServiceIds)
        {
            foreach (var employee in await employeeClient.GetQualifiedEmployeesAsync(serviceId, cancellationToken))
            {
                qualified[employee.EmployeeId] = employee;
            }
        }

        return qualified.Values.Where(e => e.IsActive).ToList();
    }

    private static async Task<HandleChatMessageResult> RespondAndPersist(
        ChatSession session,
        IChatSessionRepository repository,
        string reply,
        IReadOnlyList<AppointmentSuggestionDto> suggestions,
        bool success,
        string? errorMessage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        session.AddMessage(ChatRole.Assistant, reply, now);
        await repository.SaveChangesAsync(cancellationToken);
        return new HandleChatMessageResult(session.Id, reply, suggestions, success, errorMessage);
    }

    private static string BuildSystemPrompt() =>
        "Du bist ein Terminvorschlags-Assistent. Du machst ausschliesslich Terminvorschlaege und legst " +
        "niemals selbst einen Termin an. Frage bei Mehrdeutigkeit nach statt zu raten. Nutze ausschliesslich " +
        "die im Kontext mitgegebenen, bereits geprueften freien Zeitfenster - erfinde keine Slots. " +
        "Antworte in der Sprache der Nutzeranfrage (Deutsch/Englisch).";

    // Requirement "Datenminimierung bei externen LLM-Aufrufen": ausschliesslich abstrakte
    // Zeitfenster/Leistungstyp/Mitarbeiter-Kennung - keine Kundenhistorie, keine internen Notizen.
    private static string BuildMinimizedUserContext(IReadOnlyList<AppointmentSuggestionDto> suggestions) =>
        string.Join(
            "\n",
            suggestions.Select(s =>
                $"Mitarbeiter {s.EmployeeId}, Leistung {s.ServiceName}, frei {s.StartTime:O}-{s.EndTime:O}"));
}
