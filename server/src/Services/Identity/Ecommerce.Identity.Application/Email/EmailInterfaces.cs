using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Email;

/// <summary>The emails Identity was asked to send (specs/060).</summary>
public interface IOutgoingEmailRepository
{
    /// <summary>
    /// Keeps a request, once: <c>INSERT ... ON CONFLICT DO NOTHING</c> on its id, so a redelivered
    /// <c>EmailRequested</c> adds nothing and sends nothing twice. True when this call kept it.
    /// </summary>
    Task<bool> QueueAsync(OutgoingEmail email, CancellationToken cancellationToken = default);

    /// <summary>Adds an email to the context, so it is written by the caller's next save - with the change it is about.</summary>
    void Stage(OutgoingEmail email);

    /// <summary>
    /// The pending emails due by <paramref name="now"/>, oldest first, LOCKED (<c>FOR UPDATE SKIP LOCKED</c>) -
    /// so two instances never send one row. Must run inside a transaction; the lock is what the caller's
    /// changes are saved under.
    /// </summary>
    Task<List<OutgoingEmail>> ClaimDueAsync(DateTime now, int batch, CancellationToken cancellationToken = default);

    Task<OutgoingEmail?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>One email with its recipient's address, for an administrator (specs/087).</summary>
    Task<(OutgoingEmail Email, string? RecipientEmail)?> GetWithRecipientAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of the emails in one state, newest first, each with its recipient's address - only recipients whose
    /// address contains <paramref name="search"/> when one is given (specs/087).
    /// </summary>
    Task<(List<(OutgoingEmail Email, string? RecipientEmail)> Items, int Total)> PageAsync(
        OutgoingEmailStatus status, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// A failed email back to pending, due now, from its first attempt - one guarded <c>UPDATE ... WHERE "Status" =
    /// 'Failed'</c>, so it is false for one that is not failed any more (specs/087).
    /// </summary>
    Task<bool> TryRetryAsync(Guid id, DateTime now, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Hands one message to a mail server. Throws when it cannot - the caller retries later.</summary>
public interface IEmailTransport
{
    /// <summary>
    /// Sends <paramref name="html"/> with <paramref name="text"/> as its plain-text alternative
    /// (<c>multipart/alternative</c>, specs/077): a mail program that shows no HTML shows the text.
    /// </summary>
    Task SendAsync(string to, string subject, string text, string html, CancellationToken cancellationToken = default);
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Where a link in an email points - the storefront a person opens.</summary>
    public string StorefrontUrl { get; set; } = "http://localhost:8088";

    /// <summary>After this many failed attempts an email is <c>Failed</c>, kept with its last error.</summary>
    public int MaxAttempts { get; set; } = 12;

    /// <summary>How many emails one sweep sends at most.</summary>
    public int Batch { get; set; } = 50;
}
