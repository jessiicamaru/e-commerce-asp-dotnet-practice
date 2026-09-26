using Ecommerce.Catalog.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Catalog.Application.Products.Images;

/// <summary>Images the store holds that no row can name (specs/033). Changes nothing.</summary>
public record FindOrphanImagesQuery : IRequest<OrphanImageReport>;

/// <summary>Removes them. A separate, explicit act - nothing deletes as a side effect of asking.</summary>
public record RemoveOrphanImagesCommand : IRequest<OrphanImageReport>;

/// <param name="Scanned">What the store holds, excluding its own bookkeeping.</param>
/// <param name="LiveKeys">
/// How many keys the catalogue currently names. <b>Published so a nonsensical answer is visible as
/// one</b>: zero live keys against a full store means the database read went wrong, and a reader
/// can see that without knowing the implementation.
/// </param>
public record OrphanImageReport(
    int GraceHours,
    int Scanned,
    int LiveKeys,
    IReadOnlyList<OrphanImage> Orphans,
    long OrphanBytes,
    IReadOnlyList<string> Failed,
    string Note);

public record OrphanImage(string Key, long Bytes, DateTimeOffset LastModified, double AgeHours);

public class OrphanImageOptions
{
    public const string SectionName = "ProductImages";

    /// <summary>
    /// How old a file must be before it can be called an orphan.
    /// </summary>
    /// <remarks>
    /// <b>Not chosen to cover the upload window</b>, which is milliseconds: an upload writes the
    /// bytes and then switches the row, and in between the file exists with no row naming it.
    /// Almost any value covers that. A day is chosen to cover the OPERATOR - a file created this
    /// morning being deleted this afternoon turns out to have been a migration somebody was in the
    /// middle of. Lower it deliberately, never silently.
    /// </remarks>
    public int OrphanGraceHours { get; init; } = 24;
}

/// <summary>
/// The reconciliation both the report and the reclaim use.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>The live keys are read FIRST, and a failure is fatal.</b> If the catalogue read throws and
/// this carried on with an empty set, every file in the store would become a candidate and the
/// reclaim would remove the entire catalogue's images. Nothing about that is recoverable. The order
/// here exists because a test demands it.
/// </para>
/// </remarks>
public class OrphanImageScan(
    ILiveImageKeys products,
    IProductImageStore store,
    IOptions<OrphanImageOptions> options)
{
    private readonly ILiveImageKeys _products = products;
    private readonly IProductImageStore _store = store;
    private readonly OrphanImageOptions _options = options.Value;

    public const string OneInstanceNote =
        "One Catalog instance is assumed (specs/019). With two, each sees only its own directory "
        + "and would report the other's images as orphans.";

    public const string SharedNote =
        "The store is shared object storage (specs/079): every Catalog instance reads and writes the same bucket, "
        + "so this report is the same whichever one answers it.";

    /// <summary>What the report says about the store it read - a warning only when the store is one instance's own.</summary>
    public string Note => _store.SharedAcrossInstances ? SharedNote : OneInstanceNote;

    public async Task<(List<OrphanImage> Orphans, int Scanned, int LiveKeys, int GraceHours)> RunAsync(
        CancellationToken cancellationToken)
    {
        // FIRST, and nothing catches it. An empty set here makes every file a candidate.
        var live = await _products.GetLiveImageKeysAsync(cancellationToken);

        var grace = TimeSpan.FromHours(_options.OrphanGraceHours);
        var now = DateTimeOffset.UtcNow;

        var orphans = new List<OrphanImage>();
        var scanned = 0;

        await foreach (var stored in _store.ListAsync(cancellationToken))
        {
            scanned++;

            // A key a row names. The whole point.
            if (live.Contains(stored.Key))
            {
                continue;
            }

            var age = now - stored.LastModified;

            // An upload writes the bytes and THEN switches the row. In between the file exists and
            // no row names it - indistinguishable from an orphan, and deleting it destroys an image
            // about to become live.
            if (age < grace)
            {
                continue;
            }

            orphans.Add(new OrphanImage(
                stored.Key, stored.Size, stored.LastModified, Math.Round(age.TotalHours, 1)));
        }

        return (orphans, scanned, live.Count, _options.OrphanGraceHours);
    }
}

public class FindOrphanImagesQueryHandler(OrphanImageScan scan)
    : IRequestHandler<FindOrphanImagesQuery, OrphanImageReport>
{
    private readonly OrphanImageScan _scan = scan;

    public async Task<OrphanImageReport> Handle(FindOrphanImagesQuery request, CancellationToken cancellationToken)
    {
        var (orphans, scanned, live, grace) = await _scan.RunAsync(cancellationToken);

        return new OrphanImageReport(
            grace, scanned, live, orphans, orphans.Sum(o => o.Bytes), [], _scan.Note);
    }
}

/// <remarks>
/// ⚠️ <b>It takes no key list.</b> A request naming keys would delete whatever it was told to, and
/// the caller's list is minutes old by the time somebody has read the report and decided - long
/// enough for an upload to make one of those keys live. This re-reconciles and removes what IT
/// finds; the report is advice, not an instruction.
/// </remarks>
public class RemoveOrphanImagesCommandHandler(
    OrphanImageScan scan,
    IProductImageStore store,
    IProductRepository products,
    ILogger<RemoveOrphanImagesCommandHandler> logger,
    IAuditTrail audit)
    : IRequestHandler<RemoveOrphanImagesCommand, OrphanImageReport>
{
    private readonly IAuditTrail _audit = audit;

    private readonly OrphanImageScan _scan = scan;
    private readonly IProductImageStore _store = store;
    private readonly IProductRepository _products = products;
    private readonly ILogger<RemoveOrphanImagesCommandHandler> _logger = logger;

    public async Task<OrphanImageReport> Handle(
        RemoveOrphanImagesCommand request, CancellationToken cancellationToken)
    {
        var (candidates, scanned, live, grace) = await _scan.RunAsync(cancellationToken);

        var removed = new List<OrphanImage>();
        var failed = new List<string>();

        foreach (var orphan in candidates)
        {
            try
            {
                await _store.DeleteAsync(orphan.Key, cancellationToken);
                removed.Add(orphan);
            }
            catch (Exception ex)
            {
                // One key the store will not give up must not stop the rest. It is reported so the
                // number does not silently disagree with the report that preceded it.
                _logger.LogWarning(ex, "Could not remove orphan image {Key}.", orphan.Key);
                failed.Add(orphan.Key);
            }
        }

        if (removed.Count > 0)
        {
            _logger.LogWarning(
                "Reclaimed {Count} orphaned image(s), {Bytes} byte(s).", removed.Count, removed.Sum(o => o.Bytes));
        }

// Destroying files is what the System log exists for (specs/041) - by whom, and how many.
if (removed.Count > 0)
{
    await _audit.RecordAsync(
        AuditCategory.System, "OrphanImagesReclaimed", "ImageStore", null,
        $"Reclaimed {removed.Count} orphaned image(s), {removed.Sum(o => o.Bytes)} byte(s)",
        after: new { Keys = removed.Select(o => o.Key), Failed = failed },
        cancellationToken: cancellationToken);
    await _products.SaveChangesAsync(cancellationToken);
}
        return new OrphanImageReport(
            grace, scanned, live, removed, removed.Sum(o => o.Bytes), failed, _scan.Note);
    }
}
