# Change: [Feature Name]

> **Status:** proposed | in-progress | archived
> Ablage: `UserStories/<feature-name>.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-<feature-name>.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** [Geschäftlicher Nutzen / Problem]
- **What Changes:** [Kurze Liste der Änderungen; Breaking Changes mit **BREAKING** markieren]
- **Impact:** [Betroffene Capabilities, Bounded Contexts, Layer]

## 2. User Stories
- Als [Rolle] möchte ich [Aktion], damit [Nutzen].

## 3. Requirements (Delta)

Formulierung mit **SHALL/MUST**. Jede Requirement hat mindestens ein Scenario (WHEN/THEN). Jedes Scenario wird ein Test (TDD).

### ADDED Requirements

#### Requirement: [Titel]
Das System SHALL [Fähigkeit].

##### Scenario: [Nutzeraktion / Fall]
- **GIVEN** [Ausgangslage, optional]
- **WHEN** [Auslöser]
- **THEN** [erwartetes Ergebnis]

### MODIFIED Requirements
<!-- Vollständigen neuen Requirement-Text angeben, nicht nur den Diff. Entfällt, wenn nicht relevant. -->

### REMOVED Requirements
<!-- Requirement + Begründung/Migration. Entfällt, wenn nicht relevant. -->

## 4. UI/UX Requirements
- **Komponenten**: [Liste der DaisyUI/Angular Standalone Components]
- **State**: [Welcher State muss im Frontend verwaltet werden?]
- **i18n / a11y**: [DE/EN-Keys, ARIA/Keyboard-Anforderungen]

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung**: [Benötigte Rollen/Claims via Microsoft Identity]
- **Endpoints**:
  - `GET /api/[resource]`: [Beschreibung]
  - `POST /api/[resource]`: [Beschreibung]
- **Modelle/Entitäten (PostgreSQL)**:
  - `[EntityName]`: [Felder, z.B. Id (Guid), Name (string)]

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: [Ja/Nein]
- **Prompt/System Message**: [Falls relevant]

## 7. Design (Technischer Ansatz)
[Architekturentscheidungen, Alternativen, Risiken; bei größeren Entscheidungen Verweis auf ADR in `docs/adr/`]

## 8. Tasks (Implementierungs-Checkliste)
- [ ] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/`
- [ ] 2. Backend implementieren (Domain → Application → Infrastructure → Web)
- [ ] 3. Frontend implementieren (Angular 22, DaisyUI 5, i18n, ARIA)
- [ ] 4. E2E-/Compliance-Tests (QA) grün
- [ ] 5. Doku/OpenAPI aktualisiert

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün.
- [ ] Backend API (C# 14) ist implementiert und getestet (TUnit).
- [ ] Angular 22 Komponente ist implementiert und gestylt (DaisyUI 5).
- [ ] E2E Tests (QA Agent) sind grün.
- [ ] Swagger/OpenAPI Compliance ist gegeben.
- [ ] Spec ist archiviert (Status `archived`, Requirements ggf. in `UserStories/specs/<capability>.openspec.md` übernommen).
