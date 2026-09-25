namespace Ecommerce.Application.Auth.SignInThrottling;

/// <summary>
/// Wrong passwords counted per email (specs/062): after <see cref="SignInOptions.MaxFailures"/> within
/// <see cref="SignInOptions.WindowMinutes"/>, every sign-in for that email waits
/// <see cref="SignInOptions.CooldownMinutes"/>.
/// </summary>
/// <remarks>
/// A short pause, deliberately not the moderation lock (specs/043): a lock anybody could trigger would let
/// anybody shut anybody out. Keyed on the email, so an unknown address behaves like a real one (#28).
/// </remarks>
public interface ISignInThrottle
{
    /// <summary>When the pause on this email ends, or null when there is none.</summary>
    Task<DateTime?> BlockedUntilAsync(string emailKey, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts one wrong password in ONE statement. <see cref="FailureOutcome.Paused"/> is true for the call
    /// that started a pause - and only that one, however many arrive at once.
    /// </summary>
    Task<FailureOutcome> RecordFailureAsync(string emailKey, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>The right password, or a reset: the count starts again.</summary>
    Task ClearAsync(string emailKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes rows with no pause running and a window long over; returns how many.</summary>
    Task<int> PurgeStaleAsync(DateTime now, CancellationToken cancellationToken = default);
}

public sealed record FailureOutcome(bool Paused, DateTime? BlockedUntil);

public sealed class SignInOptions
{
    public const string SectionName = "SignIn";

    /// <summary>Wrong passwords for one email, within the window, that start a pause.</summary>
    public int MaxFailures { get; set; } = 5;

    public int WindowMinutes { get; set; } = 15;

    /// <summary>How long a pause lasts.</summary>
    public int CooldownMinutes { get; set; } = 5;

    public TimeSpan Window => TimeSpan.FromMinutes(WindowMinutes);

    public TimeSpan Cooldown => TimeSpan.FromMinutes(CooldownMinutes);

    /// <summary>Everything wrong with these settings; empty when they are usable.</summary>
    public IEnumerable<string> Problems()
    {
        if (MaxFailures < 1) yield return $"{SectionName}:MaxFailures must be at least 1 (was {MaxFailures}).";
        if (WindowMinutes < 1) yield return $"{SectionName}:WindowMinutes must be at least 1 (was {WindowMinutes}).";
        if (CooldownMinutes < 1) yield return $"{SectionName}:CooldownMinutes must be at least 1 (was {CooldownMinutes}).";
    }
}
