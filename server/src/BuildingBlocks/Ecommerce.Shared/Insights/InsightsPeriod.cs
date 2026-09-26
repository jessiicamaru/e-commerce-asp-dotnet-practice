using FluentValidation;

namespace Ecommerce.Shared.Insights;

/// <summary>
/// What a period means to every insight (specs/055, #125): <b>whole days</b>, from the day <c>from</c> falls on to
/// the day <c>to</c> falls on, both included - the shop's days, in its own time zone since specs/082 (#168), where
/// before they were UTC days and a Vietnamese morning counted on the day before.
/// </summary>
/// <remarks>
/// <para>
/// Revenue is grouped by day, views are counted by day and the Overview draws one bar per day, so a period
/// cut at an instant always leaves one bar counting part of a day. Before this, Order cut at instants and
/// Catalog at days, only revenue had a limit, and the Overview's "last 7 days" touched eight dates while its
/// chart drew seven - the earliest day's revenue was in the totals with no bar.
/// </para>
/// <para>
/// The parameters stay <see cref="DateTime"/>, so nothing that already calls these endpoints breaks: a time
/// is snapped to its day.
/// </para>
/// </remarks>
/// <param name="Start">The instant the first day begins in the shop's zone, in UTC.</param>
/// <param name="End"><b>Exclusive</b>: the instant after the last day ends, so <c>t &gt;= Start &amp;&amp; t &lt; End</c> is the period.</param>
public readonly record struct InsightsPeriod(DateOnly FirstDay, DateOnly LastDay, DateTime Start, DateTime End)
{
    public const int MaxDays = 366;
    public const int DefaultDays = 30;

    /// <summary>How many days, both ends counted.</summary>
    public int Days => LastDay.DayNumber - FirstDay.DayNumber + 1;

    /// <summary>
    /// The period a request names, in <paramref name="calendar"/>'s days; the last <see cref="DefaultDays"/> days,
    /// today included, by default.
    /// </summary>
    public static InsightsPeriod Resolve(DateTime? from, DateTime? to, DateTime now, InsightsCalendar calendar)
    {
        var last = calendar.DayOf(to ?? now);
        var first = from is { } f ? calendar.DayOf(f) : last.AddDays(-(DefaultDays - 1));
        return new InsightsPeriod(first, last, calendar.StartOf(first), calendar.StartOf(last.AddDays(1)));
    }
}

public static class InsightsPeriodRules
{
    /// <summary>
    /// The one validation every insight query runs: <c>from</c> not after <c>to</c> (a single day is a
    /// period), and at most <see cref="InsightsPeriod.MaxDays"/> days.
    /// </summary>
    public static void ValidPeriod<T>(this AbstractValidator<T> validator, Func<T, DateTime?> from, Func<T, DateTime?> to, InsightsCalendar calendar)
    {
        // In the shop's days, as the handler resolves it (specs/082): two instants on one Hanoi day can straddle a UTC
        // midnight, and counted in UTC days "from" would fall after "to".
        validator.RuleFor(x => InsightsPeriod.Resolve(from(x), to(x), DateTime.UtcNow, calendar))
            .Must(p => p.FirstDay <= p.LastDay)
            .WithName("Period")
            .WithMessage("The period must not start after it ends.")
            .Must(p => p.Days <= InsightsPeriod.MaxDays)
            .WithMessage($"The period can be at most {InsightsPeriod.MaxDays} days.");
    }
}
