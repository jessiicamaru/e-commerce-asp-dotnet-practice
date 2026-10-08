namespace Ecommerce.Catalog.Application.Common.Interfaces;

/// <summary>
/// What the catalogue's public reads are answered from when the same question was asked recently (specs/157, #361).
/// Emptied after every committed write to the catalogue's own tables, whichever path made it, so an anonymous reader never
/// sees a catalogue older than the last commit on this instance.
/// </summary>
public interface ICatalogueReadCache
{
    ValueTask EvictAsync(CancellationToken cancellationToken = default);
}
