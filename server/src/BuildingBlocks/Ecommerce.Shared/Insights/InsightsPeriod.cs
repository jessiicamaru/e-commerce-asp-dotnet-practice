using FluentValidation;

namespace Ecommerce.Shared.Insights;

/// <summary>
/// What a period means to every administrator insight (specs/055, #125): <b>whole UTC days</b>, from the day
/// <c>from</c> falls on to the day <c>to</c> falls on, both included.
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
public readonly record struct InsightsPeriod(DateOnly FirstDay, DateOnly LastDay)
{
    public const int MaxDays = 366;
    public const int DefaultDays = 30;

    /// <summary>The first day, at midnight UTC.</summary>
    public DateTime Start => FirstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary><b>Exclusive</b>: midnight UTC after the last day, so <c>t &gt;= Start &amp;&amp; t &lt; End</c> is the period.</summary>
    public DateTime End => LastDay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>How many days, both ends counted.</summary>
    public int Days => LastDay.DayNumber - FirstDay.DayNumber + 1;

    /// <summary>The period a request names; the last <see cref="DefaultDays"/> days, today included, by default.</summary>
    public static InsightsPeriod Resolve(DateTime? from, DateTime? to, DateTime now)
    {
        var last = Day(to ?? now);
        var first = from is { } f ? Day(f) : last.AddDays(-(DefaultDays - 1));
        return new InsightsPeriod(first, last);
    }

    private static DateOnly Day(DateTime value) => DateOnly.FromDateTime(value.Kind == DateTimeKind.Unspecified ? value : value.ToUniversalTime());
}

public static class InsightsPeriodRules
{
    /// <summary>
    /// The one validation every insight query runs: <c>from</c> not after <c>to</c> (a single day is a
    /// period), and at most <see cref="InsightsPeriod.MaxDays"/> days.
    /// </summary>
    public static void ValidPeriod<T>(this AbstractValidator<T> validator, Func<T, DateTime?> from, Func<T, DateTime?> to)
    {
        validator.RuleFor(x => InsightsPeriod.Resolve(from(x), to(x), DateTime.UtcNow))
            .Must(p => p.FirstDay <= p.LastDay)
            .WithName("Period")
            .WithMessage("The period must not start after it ends.")
            .Must(p => p.Days <= InsightsPeriod.MaxDays)
            .WithMessage($"The period can be at most {InsightsPeriod.MaxDays} days.");
    }
}
