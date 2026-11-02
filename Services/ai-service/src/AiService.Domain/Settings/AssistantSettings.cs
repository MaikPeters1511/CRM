using AiService.Domain.Common;

namespace AiService.Domain.Settings;

/// <summary>
/// Singleton-Einstellungen des KI-Assistenten (ki-einstellungen-backend.openspec.md). Persistiert in
/// <c>ai_db</c> (eigene Tabelle <c>assistant_settings</c>, genau eine Zeile mit <see cref="WellKnownId"/>).
/// Enthaelt bewusst KEINEN API-Key (docs/adr/0004-ki-assistent-einstellungen-laufzeit.md) - der Key
/// bleibt ausschliesslich Secret/Environment-Variable.
/// </summary>
public sealed class AssistantSettings : Entity
{
    /// <summary>Fester Schluessel der einzigen Zeile dieser Tabelle.</summary>
    public static readonly Guid WellKnownId = Guid.Parse("00000000-0000-0000-0000-00000000a55e");

    public bool Enabled { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public string QdrantBaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// <c>true</c>, solange der zuletzt per Admin-<c>PUT</c> gesetzte Provider/Modell/Qdrant-URL-Wert
    /// noch nicht durch einen Neustart von ai-service aktiv gemacht wurde
    /// (docs/adr/0004, Abschnitt "Decision").
    /// </summary>
    public bool RestartRequired { get; private set; }

    public DateTimeOffset? LastIndexSyncAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private AssistantSettings()
    {
    }

    private AssistantSettings(
        Guid id, bool enabled, string provider, string model, string qdrantBaseUrl,
        bool restartRequired, DateTimeOffset? lastIndexSyncAt, DateTimeOffset updatedAt) : base(id)
    {
        Enabled = enabled;
        Provider = provider;
        Model = model;
        QdrantBaseUrl = qdrantBaseUrl;
        RestartRequired = restartRequired;
        LastIndexSyncAt = lastIndexSyncAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>Erstanlage beim ersten Start von ai-service - Werte aus der aktuellen <c>IConfiguration</c>.</summary>
    public static AssistantSettings SeedFromStartupConfig(
        string provider, string model, string qdrantBaseUrl, DateTimeOffset now) =>
        new(WellKnownId, enabled: true, provider, model, qdrantBaseUrl, restartRequired: false, lastIndexSyncAt: null, updatedAt: now);

    /// <summary>
    /// Bei jedem weiteren Start aufgerufen: gleicht Provider/Modell/Qdrant-URL mit den tatsaechlich
    /// jetzt aktiven, aus <c>IConfiguration</c> gelesenen Werten ab - ein Neustart hat etwaige
    /// <see cref="RestartRequired"/>-Aenderungen damit uebernommen. <see cref="Enabled"/> und
    /// <see cref="LastIndexSyncAt"/> bleiben unangetastet (Admin-Entscheidung ueberlebt Neustarts).
    /// </summary>
    public void ReconcileWithStartupConfig(string provider, string model, string qdrantBaseUrl, DateTimeOffset now)
    {
        Provider = provider;
        Model = model;
        QdrantBaseUrl = qdrantBaseUrl;
        RestartRequired = false;
        UpdatedAt = now;
    }

    /// <summary>
    /// Wendet eine Admin-Aenderung per <c>PUT /api/assistant/settings</c> an. <paramref name="enabled"/>
    /// wirkt sofort (live gelesen vor jedem Chat-Request). Provider/Modell/Qdrant-URL werden persistiert,
    /// setzen aber <see cref="RestartRequired"/>, sobald sich mindestens einer der drei Werte aendert.
    /// </summary>
    public void ApplyAdminUpdate(bool enabled, string provider, string model, string qdrantBaseUrl, DateTimeOffset now)
    {
        Enabled = enabled;

        if (Provider != provider || Model != model || QdrantBaseUrl != qdrantBaseUrl)
        {
            Provider = provider;
            Model = model;
            QdrantBaseUrl = qdrantBaseUrl;
            RestartRequired = true;
        }

        UpdatedAt = now;
    }

    public void RecordIndexSync(DateTimeOffset at) => LastIndexSyncAt = at;
}
