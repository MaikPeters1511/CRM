namespace AiService.Application.Settings.TriggerIndexSync;

/// <summary>
/// <c>POST /api/assistant/settings/sync-now</c> - Admin-only. Stoesst den bestehenden
/// Qdrant-Konsistenz-Sync (docs/adr/0003, Abschnitt 3) sofort an, statt auf den naechsten
/// taeglichen Lauf zu warten.
/// </summary>
public sealed record TriggerIndexSyncCommand;

public sealed record TriggerIndexSyncResult(DateTimeOffset? PreviousSyncAt, DateTimeOffset SyncedAt);
