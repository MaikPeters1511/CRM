using AiService.Application.Abstractions;
using AiService.Application.Chat.HandleChatMessage;

namespace AiService.Tests.Application;

public class FreeSlotCalculatorTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    // Scenario: Vorschlag berücksichtigt bestehende Kollisionen
    [Test]
    public async Task ComputeFreeWindows_WithBookedSlotInMiddleOfWorkingHours_ExcludesOverlap()
    {
        var working = new[] { new WorkingHoursWindowDto(Day.AddHours(9), Day.AddHours(12)) };
        var booked = new[] { new BookedSlotDto(Day.AddHours(10), Day.AddHours(11)) };

        var freeWindows = FreeSlotCalculator.ComputeFreeWindows(working, booked, slotDurationMinutes: 60);

        var hasOverlapWithBookedSlot = freeWindows.Any(w => w.Start < Day.AddHours(11) && w.End > Day.AddHours(10));
        var hasSlotBeforeBooking = freeWindows.Any(w => w.Start == Day.AddHours(9) && w.End == Day.AddHours(10));
        var hasSlotAfterBooking = freeWindows.Any(w => w.Start == Day.AddHours(11) && w.End == Day.AddHours(12));

        await Assert.That(hasOverlapWithBookedSlot).IsFalse();
        await Assert.That(hasSlotBeforeBooking).IsTrue();
        await Assert.That(hasSlotAfterBooking).IsTrue();
    }

    [Test]
    public async Task ComputeFreeWindows_WithoutBookedSlots_ReturnsFullWorkingWindowSliced()
    {
        var working = new[] { new WorkingHoursWindowDto(Day.AddHours(9), Day.AddHours(11)) };

        var freeWindows = FreeSlotCalculator.ComputeFreeWindows(working, [], slotDurationMinutes: 60);

        await Assert.That(freeWindows).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ComputeFreeWindows_WithFullyBookedWindow_ReturnsNoFreeSlots()
    {
        var working = new[] { new WorkingHoursWindowDto(Day.AddHours(9), Day.AddHours(10)) };
        var booked = new[] { new BookedSlotDto(Day.AddHours(9), Day.AddHours(10)) };

        var freeWindows = FreeSlotCalculator.ComputeFreeWindows(working, booked, slotDurationMinutes: 60);

        await Assert.That(freeWindows).IsEmpty();
    }
}
