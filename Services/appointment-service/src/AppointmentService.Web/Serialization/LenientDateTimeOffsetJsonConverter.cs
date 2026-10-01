using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppointmentService.Web.Serialization;

/// <summary>
/// Tests/E2E/README.md, Bug 9: <c>System.Text.Json</c>'s default <see cref="DateTimeOffset"/>
/// converter interprets an ISO-8601 string WITHOUT an explicit UTC offset using the executing
/// server's LOCAL time zone, not UTC. Npgsql/PostgreSQL's <c>timestamp with time zone</c> columns
/// only accept <see cref="DateTimeOffset"/> values with <c>Offset == 0</c>, so any non-UTC offset
/// throws at write time. The real Angular form
/// (<c>Clients/crm-web/src/app/appointments/presentation/appointment-form/appointment-form.component.ts</c>)
/// deliberately sends offset-less local datetime strings (e.g. <c>"2026-10-12T10:00:00"</c>) -
/// this CRM has no per-tenant/user time zone concept, so the pragmatic, documented contract is:
/// "no offset in the request = UTC". This converter makes that assumption explicit for the Web
/// layer's <c>DateTimeOffset</c> request fields (<c>CreateAppointmentRequest.Start/End</c>,
/// <c>RescheduleAppointmentRequest.NewStart/NewEnd</c>) without changing the JSON API shape -
/// an explicit offset (e.g. <c>"Z"</c> or <c>"+02:00"</c>) is still honoured and normalised to UTC.
/// </summary>
public sealed class LenientDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Expected a non-empty ISO-8601 date/time string.");
        }

        // AssumeUniversal: only takes effect when the string carries NO offset/zone info, treating
        // it as UTC. AdjustToUniversal: normalises any explicit offset to UTC (Offset == 0), which
        // is exactly what Npgsql requires when writing to 'timestamp with time zone' columns.
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var result))
        {
            throw new JsonException($"'{value}' is not a valid ISO-8601 date/time.");
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
