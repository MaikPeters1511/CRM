# 0004. Identity-Security-Erweiterung: Persistenz, MFA (TOTP), Passkeys (WebAuthn), Device Trust, Audit-Log

> **Status:** accepted
> **Datum:** 2026-11-02
> **Autor:** architect-agent
> **Betrifft:** `UserStories/identity-mfa-totp.openspec.md`, `UserStories/identity-passkeys-webauthn.openspec.md`, `UserStories/identity-device-trust.openspec.md`, `UserStories/identity-audit-log.openspec.md`, `UserStories/specs/identity.openspec.md`, `docs/adr/0001-microservices-architektur-crm.md`

## Context

Der `identity-service` ist bisher ein schlanker, zustandsloser JWT-Issuer ohne eigene Datenbank: zwei Testnutzer werden In-Memory pro Prozessstart geseedet (`InMemorySeedUserRepository`), `LoginHandler` prüft Passwort und stellt direkt ein JWT aus (siehe `UserStories/specs/identity.openspec.md`, Abschnitt "Bewusste Vereinfachungen"). Vier parallel vorgelegte Change-Specs erweitern diese Basis gleichzeitig um sicherheitsrelevante Fähigkeiten:

- **MFA/TOTP** (`identity-mfa-totp.openspec.md`): führt als Grundlage für alle vier Changes erstmals eine eigene PostgreSQL-Datenbank (`identity_db`) ein und macht den Login zweistufig.
- **Passkeys/WebAuthn** (`identity-passkeys-webauthn.openspec.md`): alternativer, passwortloser Login-Weg, baut auf `identity_db` auf.
- **Device Trust & Security Policy** (`identity-device-trust.openspec.md`): Geräte-Fingerprinting, E-Mail-Bestätigung bei unbekanntem Gerät, org-weite Sicherheitsrichtlinie (MFA-Pflicht, Passwort-Policy, Account-Lockout), baut auf `identity_db` auf.
- **Audit-Log** (`identity-audit-log.openspec.md`): `IAuditLogger` als gemeinsames Application-Interface, das von den drei anderen Changes referenziert wird; baut ebenfalls auf `identity_db` auf.

Alle vier Specs markieren wichtige technische Festlegungen (Persistenz-Strategie, Bibliothekswahl, Challenge-Speicherung, Secret-Verschlüsselung, Verifizierungs-Notifier, Login-Reihenfolge, Audit-Persistenz) explizit als vom Architect-Agent verbindlich zu entscheiden, da sie service-übergreifend konsistent sein müssen (alle vier Changes schreiben gegen dieselbe `identity_db`, drei davon schreiben gegen denselben `LoginHandler`-Einstiegspunkt) und die bestehende Clean-Architecture-/Defense-in-Depth-Struktur (`docs/adr/0001`, Abschnitt 4 und 6) nicht verletzen dürfen. Ohne verbindliche, gemeinsame Festlegung besteht das Risiko von vier widersprüchlichen Einzellösungen (z.B. unterschiedliche Challenge-Speicherung, doppelte Migrations-Infrastruktur, inkonsistente Login-Flow-Reihenfolge).

## Decision

### 1. Persistenz: `identity_db` (PostgreSQL, EF Core)

- `identity-service` erhält eine eigene PostgreSQL-Instanz `identity_db`, analog zu `customer_db`/`employee_db`/`appointment_db` (ADR 0001, Abschnitt 1 und 7: Database per Service, kein Shared-Schema). Aufnahme als eigene Ressource im .NET Aspire `AppHost` via `WithReference`, kein hartkodierter Connection String (`CLAUDE.md`, Abschnitt Sicherheit/Infrastruktur).
- **Clean Architecture bleibt strikt eingehalten:** `User` bleibt Aggregate Root in `IdentityService.Domain` ohne EF-Core-Abhängigkeit. Die Infrastructure-Schicht bekommt ein eigenes Persistence-Model (`IdentityDbContext` in `IdentityService.Infrastructure.Persistence`, analog `EmployeeDbContext`) mit EF-Core-Mapping-Konfiguration für `User`, `MfaCredential`, `PasskeyCredential`, `KnownDevice`, `PendingDeviceConfirmation`, `SecurityPolicy`, `AuditLogEntry`. Es wird **kein** separates DTO/Persistence-Objekt zusätzlich zur Domain-Entität eingeführt, sofern die Domain-Entität selbst EF-Core-kompatibel bleibt (private Setter + EF-Core-Konstruktor sind zulässig, wie im bestehenden `Employee`-Aggregate); eine Trennung Domain-Entität/Persistence-Model ist nur dort erforderlich, wo EF-Core-Mapping-Zwänge (z.B. `OwnsMany`) die Domain-Modellierung sonst verzerren würden.
- Migrationen werden über einen Hosted Service beim Start automatisch angewendet — Wiederverwendung des bestehenden generischen `DatabaseMigrationHostedService<TContext>`-Patterns aus `employee-service` (`Services/employee-service/src/EmployeeService.Infrastructure/DependencyInjection.cs`), instanziiert für `IdentityDbContext`. Keine Neuimplementierung eines identity-spezifischen Migrations-Hosted-Service.
- **Dev-Seed:** Beim ersten Start wird geprüft, ob die `Users`-Tabelle leer ist; falls ja, werden die zwei bekannten Testnutzer (Admin/Employee) mit identischen Anmeldedaten wie bisher per Hosted Service eingefügt (`IdentityDevUserSeedHostedService`, Vorbild `DevEmployeeSeedHostedService`), nur in `environment.IsDevelopment()` aktiv — konsistent mit dem bestehenden Dev-/Test-only-Pattern in `EmployeeService.Infrastructure.DependencyInjection`. Bestehende E2E-Tests (`Tests/E2E/tests/auth.spec.ts`) bleiben dadurch ohne Anpassung grün.
- Verantwortlich für die konkrete Mapping-Konfiguration und Migrationserstellung: `backend-agent`.

### 2. TOTP (MFA)

- Bibliothek: **OtpNet** (RFC 6238-konform), gekapselt hinter dem Application-Interface `ITotpProvider`; keine Eigenimplementierung des Algorithmus.
- Secret-Speicherung: Base32-codiertes Secret in `MfaCredential.SecretEncrypted` (Spaltenname bewusst mit `Encrypted`-Suffix, um die Erwartungshaltung an spätere Verschlüsselung architektonisch zu dokumentieren). **Verschlüsselung at-rest wird in diesem Schritt nicht implementiert** — es existiert aktuell keine Secret-Manager-/Data-Protection-Key-Infrastruktur im Projekt, deren Einführung über den Scope dieser vier Changes hinausgeht. Dies ist eine bewusste, zeitlich begrenzte Risikoakzeptanz (siehe Consequences) und keine Dauerlösung.
- **Folgeaufgabe (verbindlich vorgemerkt, nicht Teil dieser vier Changes):** Einführung der ASP.NET Core Data Protection API zur Verschlüsselung von `MfaCredential.SecretEncrypted` vor Persistierung, inkl. Schlüsselverwaltung über Environment Variable/Secret Manager. Separates ADR oder Ergänzung dieses ADR, sobald umgesetzt.
- Kein Backup-Code-Mechanismus in dieser Iteration (bewusste Vereinfachung gemäß Spec Abschnitt 7); Verlust des Authenticator-Geräts erfordert Deaktivierung via Passwort.

### 3. WebAuthn/Passkeys

- Bibliothek: **Fido2NetLib** für die gesamte WebAuthn-Server-Logik (Options-Generierung, Attestation-/Assertion-Validierung); keine Eigenimplementierung von WebAuthn/CBOR/COSE.
- **Challenge-State ist kurzlebig und wird NICHT in `identity_db` persistiert**, sondern in einem In-Memory-Cache mit TTL (wenige Minuten) gehalten. Begründung: Registrierungs- und Login-Challenges sind ausschließlich für die Dauer der jeweiligen Ceremony relevant; eine DB-Persistenz würde unnötige Schreiblast, Clean-up-Jobs und eine zusätzliche Angriffsfläche (persistente, potenziell wiederverwendbare Challenge-Datensätze) erzeugen, ohne fachlichen Mehrwert. Dasselbe Muster gilt analog für die MFA-Challenge (`identity-mfa-totp.openspec.md`, Abschnitt 7) und die Device-Confirmation-Pending-Daten — letztere werden dagegen bewusst in `identity_db` persistiert (`PendingDeviceConfirmation`), da sie eine längere Lebensdauer (E-Mail-Versand-Latenz, möglicher Mehrgeräte-Zugriff) und Admin-Nachvollziehbarkeit erfordern; eine rein In-Memory-Lösung wäre dort nicht robust gegenüber Prozess-Neustarts während der Bestätigungsfrist.
- RP-ID/Origin-Konfiguration ausschließlich über `appsettings.json`/Environment Variables je Umgebung, kein Hardcoding (`CLAUDE.md`, Abschnitt Sicherheit).

### 4. Geräte-Vertrauen

- Fingerprint-Heuristik: Hash aus User-Agent-String + IP-Adress-Bucket (z.B. /24-Subnetz), gespeichert als `FingerprintHash` — **kein Rohtext-Tracking** von User-Agent/IP-Historie über das für die Anzeige (Admin-Ansicht) notwendige Maß hinaus, aus Datenschutzgründen. `KnownDevice.UserAgent`/`LastIpRange` werden nur für die Admin-Übersicht gespeichert, nicht der vollständige, ungekürzte Rohwert jeder einzelnen Anfrage.
- `IDeviceConfirmationNotifier` als Application-Interface (analog `IReminderNotifier` aus `appointment-service`), Infrastructure-Implementierung in dieser Iteration ein reiner Log-Stub (`LogDeviceConfirmationNotifier`). Begründung: kein SMTP-Provider im Projekt konfiguriert; echter E-Mail-Versand ist explizit Folgeaufgabe eines separaten Changes (Abstimmung `devops-agent`/`sec-agent`), um den Scope dieser Sicherheits-Initiative nicht zu sprengen. Dies ist eine KNOWN SIMPLIFICATION, kein verdeckter Kompromiss.

### 5. Login-Flow-Erweiterung (Reihenfolge der Checks)

Der bestehende zustandslose `LoginHandler` (`Services/identity-service/src/IdentityService.Application/Auth/Login/LoginHandler.cs`) bleibt die alleinige Einstiegsstelle für `POST /api/auth/login` und wird um die neuen Zwischenstatus erweitert, ohne die Defense-in-Depth-Struktur aus ADR 0001 (Gateway terminiert Auth, jeder Service validiert zusätzlich selbst) zu verändern — diese Erweiterung betrifft ausschließlich den Login-Handshake *innerhalb* von `identity-service`, nicht die nachgelagerte Token-Validierung in den anderen Services.

Verbindliche Reihenfolge der Checks bei `POST /api/auth/login`:

1. **Passwort-Prüfung** (unverändert, User-Enumeration-sicher wie bisher: identische Antwort für "Nutzer unbekannt" und "Passwort falsch").
2. **Account-Lockout-Prüfung** (`identity-device-trust.openspec.md`) erfolgt vor Schritt 1 inhaltlich bereits implizit nötig (Zähler wird bei jedem Versuch unabhängig vom Ausgang betrachtet), aber die *Sperrantwort* (423 Locked) wird nur ausgegeben, wenn die Schwelle bereits vor diesem Versuch erreicht war — d.h. der Lockout-Check ist der erste Gate, noch vor der eigentlichen Passwort-Prüfung, um bei gesperrtem Account keine Rückschlüsse über Passwort-Korrektheit mehr zuzulassen.
3. **Device-Trust-Check**: Nach erfolgreicher Passwort-Prüfung wird der Geräte-Fingerprint gegen `KnownDevice` abgeglichen. Ist das Gerät unbekannt oder gesperrt (`IsRevoked`), wird der Login mit `DeviceConfirmationRequired` unterbrochen (E-Mail-Code via `IDeviceConfirmationNotifier`), **noch vor** der MFA-Prüfung.
4. **MFA-Pflicht-Check** (eigene Einstellung oder `SecurityPolicy.RequireMfa`): Ist MFA aktiv/pflichtig, wird der Login mit `MfaRequired` unterbrochen (Challenge-Token für `POST /api/auth/login/mfa`).
5. **Passkey-Login** (`POST /api/auth/passkeys/login`) ist ein paralleler, alternativer Einstiegspunkt zum passwortbasierten Flow (kein Passwort-Schritt), durchläuft aber ab dem Zeitpunkt der erfolgreichen Assertion-Validierung dieselben Schritte 2–4 (Lockout ist für Passkey-Login nicht relevant, da kein Passwort-Fehlversuchszähler greift; Device-Trust und MFA-Pflicht gelten jedoch identisch, da beide Flows dieselbe `User`-Identität authentifizieren).

**Begründung der Reihenfolge Device-Trust vor MFA:** Ein unbekanntes Gerät ist das stärkere Signal für ein potenziell kompromittiertes Credential als eine fehlende/noch nicht abgeschlossene MFA-Verifizierung an sich; die Geräte-Bestätigung per E-Mail bindet einen zweiten, vom Credential unabhängigen Kanal ein und sollte daher nicht erst nach einer bereits erfolgreichen MFA-Eingabe erfolgen (sonst hätte ein Angreifer mit Passwort *und* TOTP-Zugriff bereits einen vollständigen Login-Nachweis erbracht, bevor das Gerät überhaupt geprüft wird). Jeder Zwischenstatus (`DeviceConfirmationRequired`, `MfaRequired`) stellt ein eigenes kurzlebiges Challenge-Token aus; es wird zu keinem Zeitpunkt ein Access Token ausgestellt, bevor alle zutreffenden Gates durchlaufen sind.

### 6. Audit-Log

- `IAuditLogger` ist ein Application-Interface in `IdentityService.Application` (nicht Domain — Logging ist ein Application-Concern, kein Teil der Geschäftsregeln des `User`-Aggregats).
- Implementierung: **synchrone DB-Writes** gegen eine append-only Tabelle `AuditLogEntry` in `identity_db`, innerhalb derselben DB-Transaktion wie die auslösende Aktion, wo sinnvoll (z.B. Login). **Kein** separates Event-Sourcing/Messaging (Wolverine/RabbitMQ) für das Audit-Log. Begründung: Audit-Log und auslösende Aktion liegen im selben Service/derselben DB (kein Cross-Service-Szenario wie in ADR 0001 Abschnitt 2); es existieren aktuell keine Cross-Service-Konsumenten für Audit-Events. Diese Entscheidung ist konsistent mit dem bewusst schlanken, messaging-freien Architekturstil von `identity-service` (siehe `LoginHandler`-Kommentar: "kein Wolverine-Messaging... da der Identity-Service... synchron antworten muss").
- Append-only wird auf Anwendungsebene durch Fehlen einer Update/Delete-Methode im Repository durchgesetzt; eine zusätzliche DB-Rollen-Härtung (`REVOKE UPDATE, DELETE`) ist empfohlen, aber kein Hard-Requirement dieser Iteration.
- Reihenfolge der Umsetzung: `identity-audit-log.openspec.md` (Interface + Entität + Basis-Implementierung) wird zuerst oder parallel-vorlaufend zu den drei anderen Changes umgesetzt, da diese `IAuditLogger` referenzieren.

### 7. Secrets-Handling

- Keine Secrets im Code (`CLAUDE.md`, Abschnitt Sicherheit). JWT-Signing-Key und `identity_db`-Connection-String bleiben wie bisher über Environment Variables/Aspire-Parameter konfiguriert, keine neue Ausnahme durch diese vier Changes.
- Künftiges Data-Protection-Schlüsselmaterial für die TOTP-Secret-Verschlüsselung (siehe Abschnitt 2, Folgeaufgabe) folgt demselben Prinzip, sobald umgesetzt.

## Consequences

**Positiv:**
- Eine einzige, für alle vier Changes verbindliche Festlegung zu `identity_db`, Login-Reihenfolge und Audit-Infrastruktur verhindert vier inkonsistente Einzellösungen und mehrfach aufgesetzte Migrations-Infrastruktur.
- Wiederverwendung bewährter Muster (`DatabaseMigrationHostedService<TContext>`, `IReminderNotifier`-artiges Notifier-Interface, Dev-Seed-Hosted-Service) reduziert Implementierungsrisiko und hält den Code konsistent zu `employee-service`/`appointment-service`.
- Klare Reihenfolge der Login-Gates (Lockout → Passwort → Device-Trust → MFA) verhindert Informationsleckagen (z.B. MFA-Bestätigung vor Geräte-Validierung) und ist für `backend-agent` eindeutig implementierbar.
- Synchrones, transaktionales Audit-Logging ohne zusätzliche Messaging-Infrastruktur passt zum bewusst schlanken Architekturstil von `identity-service` und vermeidet unnötige Komplexität.

**Negativ / Trade-offs:**
- `identity-service` verliert seine bisherige Eigenschaft als vollständig zustandsloser, DB-freier Service; Verfügbarkeit des Logins hängt künftig zusätzlich von `identity_db` ab (bewusst akzeptiert, da MFA/Passkeys/Device-Trust/Audit ohne Persistenz nicht umsetzbar sind).
- Vier parallele Changes gegen dieselbe Datenbank erhöhen das Risiko von Migrationskonflikten; verbindliche Umsetzungsreihenfolge (zuerst `identity-mfa-totp` für die DB-Grundlage, `identity-audit-log` für `IAuditLogger` parallel/vorlaufend) muss von `sm-agent`/`po-agent` bei der Sprintplanung eingehalten werden.
- Geräte-Bestätigung per Log-Stub statt echtem E-Mail-Versand ist im aktuellen Deployment für Endnutzer nicht nachvollziehbar (nur über Log-Einsicht) — Produktionsreife erfordert einen Folge-Change (SMTP-Anbindung).

**Sicherheitsannahmen, die ein menschliches Security-Review (`sec-agent`) verdienen, bevor diese Features als produktionsreif gelten:**
1. **TOTP-Secret-Verschlüsselung at-rest fehlt in dieser Iteration.** `MfaCredential.SecretEncrypted` liegt vorerst unverschlüsselt (Base32-Klartext) in `identity_db`. Ein DB-Leak würde alle aktiven MFA-Secrets kompromittieren. Verbindliche Folgeaufgabe: Data Protection API-Integration vor Produktiveinsatz.
2. **Geräte-Fingerprint-Heuristik ist pragmatisch, nicht fälschungssicher.** User-Agent + IP-Bucket lässt sich von einem Angreifer mit Kenntnis des Opfer-Setups (gleicher Browser, ähnliches Subnetz z.B. im selben Firmen-NAT) leicht nachbilden; dies ist bewusst kein robustes Device-Binding (z.B. kein TLS-Client-Zertifikat, kein Hardware-Attestation-Bezug), sondern eine Heuristik gegen Alltagsrisiken.
3. **Account-Lockout und IP-Rate-Limiting als alleinige Brute-Force-Schutzmaßnahmen** sollten auf Umgehungsmöglichkeiten (z.B. verteilte Angriffe über viele IPs bei niedrigem Lockout-Schwellenwert pro Account) geprüft werden.
4. **Neue Endpunkte ohne explizit spezifiziertes Rate Limiting** (`/api/auth/mfa/*`, `/api/auth/passkeys/*`, `/api/auth/devices/confirm`) sollten auf das gleiche oder ein angemessenes IP-basiertes Rate Limiting wie der bestehende `/api/auth/login`-Endpoint geprüft werden, insbesondere `POST /api/auth/login/mfa` und `POST /api/auth/devices/confirm` (Code-Rateerschöpfung durch Brute-Force eines 6-stelligen Codes).
5. **Fido2NetLib-Attestation-Policy akzeptiert "None"/"Self"** ohne MDS-Trust-Chain-Prüfung — angemessen für dieses interne CRM, sollte aber bei künftig strengeren Compliance-Anforderungen reevaluiert werden.
6. **Log-Stub statt echtem E-Mail-Versand für Device-Confirmation** bedeutet, dass die Sicherheitsmaßnahme im aktuellen Deployment faktisch nicht wirksam ist, bis SMTP angebunden ist — dies darf nicht als "erledigt" im Sinne eines produktiven Schutzes missverstanden werden.

**Folgeaufgaben:**
- `backend-agent`: `IdentityDbContext`, EF-Core-Mapping, `DatabaseMigrationHostedService<IdentityDbContext>`, Dev-Seed-Hosted-Service, `LoginHandler`-Erweiterung gemäß Abschnitt 5, `IAuditLogger`-Implementierung gemäß Abschnitt 6 umsetzen.
- `devops-agent`: `identity_db`-Ressource im Aspire `AppHost` ergänzen.
- `sec-agent`: Review der oben genannten fünf Sicherheitsannahmen vor Produktivfreigabe; Abstimmung SMTP-Anbindung für `IDeviceConfirmationNotifier`; Rate-Limiting-Konfiguration für neue Endpunkte.
- `architect-agent`: Folge-ADR oder Ergänzung dieses ADR, sobald Data Protection API für TOTP-Secret-Verschlüsselung umgesetzt wird.
