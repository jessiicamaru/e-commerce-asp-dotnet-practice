using Ecommerce.Catalog.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Application.Sellers;

/// <summary>
/// Records what Identity last said a shop is called (specs/027).
/// </summary>
/// <remarks>
/// <para>
/// One command behind both <c>SellerRegisteredEvent</c> and <c>SellerRenamedEvent</c>, because from
/// Catalog's side they are the same fact arriving twice: this id, this name, as at this instant. Two
/// handlers doing the same upsert would be two places to forget the guard below.
/// </para>
/// <para>
/// <b>Guarded by the timestamp, not by whether the name differs.</b> Comparing names survives a
/// duplicate delivery and fails an overtaken one: a late "Camera World" arriving after a newer
/// "Camera World HN" would win, and the catalogue would show a name the seller has already changed.
/// The same trap <c>TryRecordAvailabilityAsync</c> documents, in a second place.
/// </para>
/// </remarks>
public record RecordSellerCommand(Guid SellerId, string ShopName, DateTime ObservedAt) : IRequest<bool>;

public class RecordSellerCommandHandler(
    ISellerRepository sellers,
    ILogger<RecordSellerCommandHandler> logger) : IRequestHandler<RecordSellerCommand, bool>
{
    private readonly ISellerRepository _sellers = sellers;
    private readonly ILogger<RecordSellerCommandHandler> _logger = logger;

    public async Task<bool> Handle(RecordSellerCommand request, CancellationToken cancellationToken)
    {
        var recorded = await _sellers.TryRecordAsync(
            request.SellerId, request.ShopName.Trim(), request.ObservedAt, cancellationToken);

        if (!recorded)
        {
            // Zero rows is a NORMAL answer: a redelivery, or a message overtaken by a newer one.
            _logger.LogInformation(
                "Shop name for {SellerId} not recorded: a newer one is already stored.", request.SellerId);
        }

        return recorded;
    }
}
