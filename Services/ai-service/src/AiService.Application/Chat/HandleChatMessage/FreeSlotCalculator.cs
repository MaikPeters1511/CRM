using AiService.Application.Abstractions;

namespace AiService.Application.Chat.HandleChatMessage;

/// <summary>
/// Zieht belegte Zeitfenster (<c>appointment-service</c>-Live-Daten) von den Arbeitszeitfenstern
/// (<c>employee-service</c>-Live-Daten) ab (Requirement "Datengrundlage für Vorschläge") und
/// schneidet die verbleibenden freien Fenster auf die Leistungsdauer zu. Reine, von Infrastruktur
/// unabhaengige Berechnung - leicht isoliert testbar.
/// </summary>
public static class FreeSlotCalculator
{
    public static IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> ComputeFreeWindows(
        IReadOnlyList<WorkingHoursWindowDto> workingWindows,
        IReadOnlyList<BookedSlotDto> bookedSlots,
        int slotDurationMinutes)
    {
        var result = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var duration = TimeSpan.FromMinutes(Math.Max(slotDurationMinutes, 15));

        foreach (var window in workingWindows)
        {
            var overlapping = bookedSlots
                .Where(b => b.Start < window.End && b.End > window.Start)
                .OrderBy(b => b.Start)
                .ToList();

            var cursor = window.Start;
            foreach (var booked in overlapping)
            {
                if (booked.Start > cursor)
                {
                    AddSlicedSlots(result, cursor, booked.Start, duration);
                }

                cursor = booked.End > cursor ? booked.End : cursor;
            }

            if (cursor < window.End)
            {
                AddSlicedSlots(result, cursor, window.End, duration);
            }
        }

        return result;
    }

    private static void AddSlicedSlots(
        List<(DateTimeOffset Start, DateTimeOffset End)> result, DateTimeOffset from, DateTimeOffset to, TimeSpan duration)
    {
        var slotStart = from;
        while (slotStart + duration <= to)
        {
            result.Add((slotStart, slotStart + duration));
            slotStart += duration;
        }
    }
}
