using System.Collections.Concurrent;
using System.Security.Claims;
using Ecommerce.Contracts.Identity;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Shared.Authentication;

/// <summary>
/// Which users' access tokens stopped working, and from when (specs/065, #112) - a read model in every service
/// that validates tokens, fed by <see cref="AccessTokensRevoked"/>. Without it a banned person's token kept
/// working for up to 15 minutes after the ban.
/// </summary>
/// <remarks>
/// <para>
/// In memory, per instance. A restart forgets - and the worst case is then the old behaviour, a token living
/// out its minutes. It fails OPEN to that, never closed: nothing here can refuse a token Identity would not.
/// </para>
/// <para>
/// ⚠️ Tokens carry <c>iat</c> in whole SECONDS, so a revocation counts from the start of its second: a token
/// issued in that same second is accepted. Comparing with the exact instant would refuse the new token of the
/// very session a password change keeps, and it would refresh for ever.
/// </para>
/// </remarks>
public class RevokedAccessTokens(TimeProvider clock)
{
    /// <summary>After this, every token a revocation could refuse has expired (they live 15 minutes).</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(1);

    private readonly TimeProvider _clock = clock;
    private readonly ConcurrentDictionary<Guid, DateTime> _revokedAt = new();

    public int Count => _revokedAt.Count;

    /// <summary>Tokens of <paramref name="userId"/> issued before <paramref name="at"/> stop working. The latest instant wins.</summary>
    public void Revoke(Guid userId, DateTime at)
    {
        var utc = DateTime.SpecifyKind(at, DateTimeKind.Utc);
        _revokedAt.AddOrUpdate(userId, utc, (_, known) => utc > known ? utc : known);
        Prune();
    }

    /// <summary>Whether a token of <paramref name="userId"/> issued at <paramref name="issuedAt"/> was revoked.</summary>
    public bool IsRevoked(Guid userId, DateTime issuedAt) =>
        _revokedAt.TryGetValue(userId, out var at) && issuedAt < WholeSecond(at);

    /// <summary>The same question asked of a validated token: its <c>sub</c> and its <c>iat</c>.</summary>
    public bool IsRevoked(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId)
            || !long.TryParse(principal.FindFirstValue("iat"), out var seconds))
        {
            return false;
        }

        return IsRevoked(userId, DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime);
    }

    private static DateTime WholeSecond(DateTime at) => new(at.Ticks - at.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private void Prune()
    {
        var forgetBefore = _clock.GetUtcNow().UtcDateTime - Retention;
        foreach (var (userId, at) in _revokedAt)
        {
            if (at < forgetBefore)
            {
                _revokedAt.TryRemove(userId, out _);
            }
        }
    }
}

/// <summary>Records a revocation in this instance's list (specs/065).</summary>
public class AccessTokensRevokedConsumer(RevokedAccessTokens revoked, ILogger<AccessTokensRevokedConsumer> logger)
    : IConsumer<AccessTokensRevoked>
{
    private readonly RevokedAccessTokens _revoked = revoked;
    private readonly ILogger<AccessTokensRevokedConsumer> _logger = logger;

    public Task Consume(ConsumeContext<AccessTokensRevoked> context)
    {
        _revoked.Revoke(context.Message.UserId, context.Message.RevokedAt);
        _logger.LogInformation("Access tokens of user {UserId} issued before {RevokedAt:o} no longer work ({Reason}).",
            context.Message.UserId, context.Message.RevokedAt, context.Message.Reason);
        return Task.CompletedTask;
    }
}

public static class AccessTokenRevocationRegistration
{
    /// <summary>
    /// Every instance of <paramref name="service"/> hears every revocation (specs/065): a TEMPORARY queue per
    /// instance. A durable queue per service would hand each message to one instance only - the queue collision
    /// this codebase has met before, one level down.
    /// </summary>
    public static void AddAccessTokenRevocations(this IBusRegistrationConfigurator bus, string service)
    {
        // One name per process: the instance's own queue, deleted when it disconnects.
        var instance = Guid.NewGuid().ToString("N")[..12];
        bus.AddConsumer<AccessTokensRevokedConsumer>()
            .Endpoint(endpoint =>
            {
                endpoint.Name = $"{service}-access-revoked-{instance}";
                endpoint.Temporary = true;
            });
    }
}
