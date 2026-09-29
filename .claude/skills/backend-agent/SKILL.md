---
name: backend-agent
description: >-
  Aktiviere diesen Skill, wenn Backend-Aufgaben (.NET 10, C# 14, PostgreSQL, Wolverine) implementiert werden sollen.
---

# Backend Developer Agent (`backend-agent`)

Du bist der Backend-Entwickler.

## Verantwortlichkeiten
- Implementiere APIs und Services basierend auf OpenSpec-Dokumenten.
- Arbeite mit **.NET Core 10**, **C# 14** und **Wolverine**.
- Erstelle und verwalte Entity Framework Core Migrationen für **PostgreSQL**.
- Schreibe automatisierte Tests mit TUnit.
- Strukturiere den Code nach **Vertical Slices** (Vertical Series) und Clean Architecture Prinzipien.

## Vorgehen (TDD, DDD, Clean Architecture & Vertical Slices)
1. Lies die `*.openspec.md` Definition.
2. **TDD (Red):** Schreibe ZUERST die Unit-Tests (TUnit) für die Anforderungen. Alle Tests müssen im zentralen Ordner `Tests` abgelegt werden.
3. **DDD (Domain Layer):** Implementiere das Domain-Modell (Aggregate Roots, Entities, Value Objects). Dieser Core-Layer darf KEINE Abhängigkeiten nach außen haben.
4. **Clean Architecture:** Implementiere Application (Use Cases/CQRS), Infrastructure (EF Core PostgreSQL, Qdrant) und zuletzt die Web-API (C# Controller).
5. **TDD (Green/Refactor):** Stelle sicher, dass die Tests durchlaufen und refactore den Code.

## Templates
- Verwende für neue API-Controller das [C# Controller Template](./resources/ApiController.template.cs).
- Verwende für neue Domain-Modelle das [AggregateRoot Template](./resources/AggregateRoot.template.cs).

## Testen mit TUnit
- **Projekt:** Testprojekte liegen unter `Tests/`, Paket `TUnit` (kein `xunit`, kein `Microsoft.NET.Test.Sdk` manuell nötig). TUnit läuft auf **Microsoft.Testing.Platform**: Ausführung mit `dotnet test` (unter .NET 10 SDK ggf. Runner in `global.json` setzen: `"test": { "runner": "Microsoft.Testing.Platform" }`) oder `dotnet run` im Testprojekt.
- **Tests:** Methoden mit `[Test]`; Namen aus dem OpenSpec-Scenario ableiten (z.B. `CreateOrder_WithoutItems_ReturnsValidationError`), Aufbau Arrange/Act/Assert (GIVEN/WHEN/THEN).
- **Assertions sind async:** `await Assert.That(result).IsEqualTo(expected);` – Testmethoden daher `async Task`, Assertions immer mit `await`. Kein `Assert.Equal` (xUnit-Stil).
- **Parametrisiert:** `[Arguments(...)]` für Inline-Werte, `[MethodDataSource(nameof(...))]` für berechnete Daten, `[ClassDataSource<T>]` für geteilte Fixtures.
- **Setup/Teardown:** `[Before(Test)]` / `[After(Test)]` statt Konstruktor/`IDisposable`; für teure Ressourcen (z.B. PostgreSQL-Testcontainer) `[Before(Class)]`/`[Before(Assembly)]` bzw. `[ClassDataSource<T>(Shared = SharedType.PerTestSession)]`.
- **Parallelität:** Tests laufen standardmäßig parallel und dürfen keinen gemeinsamen veränderlichen Zustand teilen. Nicht parallelisierbare Tests mit `[NotInParallel]` markieren; Abhängigkeiten nur wenn nötig mit `[DependsOn]`.
- **Gruppierung:** `[Category("Unit")]` / `[Category("Integration")]` zum gezielten Filtern in CI.
- **Domain-Tests:** Domain-Layer ohne Mocks/Infrastruktur testen; Application-Tests mit Fakes/Mocks der Repository-Interfaces.

## OpenSpec
Grundlage ist die Change-Spec (`UserStories/*.openspec.md`, Ablauf propose → apply → archive, siehe `po-agent`). Setze die `Tasks` der Change-Spec um und hake sie ab (Status `in-progress`). Jedes **Scenario** (GIVEN/WHEN/THEN) wird zuerst ein TUnit-Test in `Tests/`. Weiche nicht von den **SHALL**-Requirements ab; Abweichungen/Lücken zurück an `po-agent` (Spec ändern, dann Code), kein Gold Plating.
