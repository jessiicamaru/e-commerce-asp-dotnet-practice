using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Shared.Insights;

/// <summary>
/// The shop's calendar (specs/082, #168): which day an instant falls on, in the shop's own time zone. Every insight
/// counts these days - an order paid at 06:30 in Hanoi is that morning's, not the previous UTC day's.
/// </summary>
/// <remarks>
/// <para>
/// <b>One zone for the shop, not the reader's.</b> Two staff in two zones must see the same numbers, and a seller's
/// day is the shop's day. <c>Insights:TimeZone</c>, an IANA id; <c>Asia/Ho_Chi_Minh</c> by default.
/// </para>
/// <para>
/// <b>Configured once, validated at startup.</b> An id the machine cannot resolve stops the service rather than
/// counting silently in UTC.
/// </para>
/// </remarks>
public sealed class InsightsCalendar
{
    public const string DefaultZone = "Asia/Ho_Chi_Minh";

    /// <summary>UTC days - what every insight counted before specs/082, and what a validator uses to count days.</summary>
    public static readonly InsightsCalendar Utc = new(TimeZoneInfo.Utc);

    public InsightsCalendar(TimeZoneInfo zone) => Zone = zone;

    public TimeZoneInfo Zone { get; }

    /// <summary>The IANA id PostgreSQL's <c>AT TIME ZONE</c> understands.</summary>
    public string ZoneId => Zone.HasIanaId ? Zone.Id : TimeZoneInfo.TryConvertWindowsIdToIanaId(Zone.Id, out var iana) ? iana : Zone.Id;

    /// <summary>
    /// The day a value falls on in the shop's zone. An instant (UTC or local kind) is converted; a value with no
    /// kind - a bare <c>2026-09-26</c> from a query string - is already a calendar day and is taken as it is.
    /// </summary>
    public DateOnly DayOf(DateTime value) =>
        DateOnly.FromDateTime(value.Kind == DateTimeKind.Unspecified
            ? value
            : TimeZoneInfo.ConvertTimeFromUtc(value.ToUniversalTime(), Zone));

    /// <summary>The instant a day begins in the shop's zone, in UTC - what a <c>timestamptz</c> column is compared with.</summary>
    public DateTime StartOf(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);

    public static InsightsCalendar For(string zoneId) => new(TimeZoneInfo.FindSystemTimeZoneById(zoneId));
}

public static class InsightsCalendarRegistration
{
    /// <summary>
    /// The shop's calendar from <c>Insights:TimeZone</c> (default <see cref="InsightsCalendar.DefaultZone"/>). Resolved
    /// here, at registration, so a zone the machine does not know stops the service at startup.
    /// </summary>
    public static IServiceCollection AddInsightsCalendar(this IServiceCollection services, IConfiguration configuration)
    {
        var zoneId = configuration["Insights:TimeZone"] is { Length: > 0 } configured ? configured : InsightsCalendar.DefaultZone;
        InsightsCalendar calendar;
        try
        {
            calendar = InsightsCalendar.For(zoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"Insights:TimeZone '{zoneId}' is not a time zone this machine knows. Use an IANA id such as '{InsightsCalendar.DefaultZone}'.", ex);
        }

        return services.AddSingleton(calendar);
    }
}
