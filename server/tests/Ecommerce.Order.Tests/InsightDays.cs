using Ecommerce.Shared.Insights;

namespace Ecommerce.Order.Tests;

/// <summary>
/// A day nobody else in this run uses, for a test that counts a whole day's revenue.
/// </summary>
/// <remarks>
/// Admin revenue sums every order on a day, and the insight tests share one database. Each test used to draw one
/// of 3,000 random days, so two of them sometimes drew the same day and one counted the other's orders: 73,700
/// where 34,100 was placed, red on a pull request that touched nothing near it. Days are now handed out ten apart
/// from a random base, so periods of a few days on either side stay a test's own. The random base keeps apart the
/// runs whose rows a local database keeps; 2050 onwards stays clear of the 2035 and 2040 ranges other tests date orders in.
/// </remarks>
internal static class InsightDays
{
    private static readonly DateOnly Base = new DateOnly(2050, 1, 1).AddDays(Random.Shared.Next(0, 500) * 1000);
    private static int _next;

    /// <summary>A fresh shop day.</summary>
    public static DateOnly Next() => Base.AddDays(Interlocked.Increment(ref _next) * 10);

    /// <summary>The instant a fresh shop day begins (Hanoi's midnight, specs/082), in UTC.</summary>
    public static DateTime NextStart() => InsightsCalendar.For(InsightsCalendar.DefaultZone).StartOf(Next());
}
