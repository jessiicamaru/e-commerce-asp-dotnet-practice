namespace Ecommerce.Shared.Exceptions;

/// <summary>
/// 429 with a sentence the caller may read, and how long to wait (specs/062) - for instance a sign-in after
/// too many wrong passwords for one email. <see cref="RetryAfter"/> becomes <c>Retry-After</c> and a
/// <c>retryAfter</c> extension, in whole seconds rounded up, so a client can say "try again in N minutes".
/// </summary>
public class TooManyRequestsException(string message, TimeSpan retryAfter) : Exception(message)
{
    public TimeSpan RetryAfter { get; } = retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter;

    /// <summary>Whole seconds, rounded up and never zero: "retry after 0" would invite an immediate retry.</summary>
    public int RetryAfterSeconds => Math.Max(1, (int)Math.Ceiling(RetryAfter.TotalSeconds));
}
