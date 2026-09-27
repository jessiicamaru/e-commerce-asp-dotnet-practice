using System.Globalization;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Notifications;
using MediatR;

namespace Ecommerce.Catalog.Application.Sellers;

/// <summary>
/// Inventory saw a sale take a variant below its line (#200, specs/102); Catalog knows whose it is and what it is called.
/// True when a seller was told.
/// </summary>
public record NotifyLowStockCommand(Guid VariantId, int QuantityAvailable) : IRequest<bool>;

public class NotifyLowStockCommandHandler(IProductRepository products, INotifier notifier) : IRequestHandler<NotifyLowStockCommand, bool>
{
    private readonly IProductRepository _products = products;
    private readonly INotifier _notifier = notifier;

    public async Task<bool> Handle(NotifyLowStockCommand request, CancellationToken cancellationToken)
    {
        var variant = await _products.GetVariantAsync(request.VariantId, cancellationToken);

        // The shop's own goods have nobody in particular to tell (research D5); an unknown variant was deleted since.
        if (variant?.Product is not { SellerId: { } seller } product)
            return false;

        var name = string.IsNullOrEmpty(variant.OptionSummary) ? product.Name : $"{product.Name} · {variant.OptionSummary}";
        await _notifier.NotifyAsync(seller, NotificationKind.StockRunningLow,
            new Dictionary<string, string>
            {
                ["product"] = name,
                ["left"] = request.QuantityAvailable.ToString(CultureInfo.InvariantCulture),
            },
            $"/shop/products/{product.Id}", cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);
        return true;
    }
}
