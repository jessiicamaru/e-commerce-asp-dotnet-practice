using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Saved;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Catalog.Application.Sellers;

/// <summary>
/// A seller's shop closed (banned) or reopened (ban lifted), as Identity announced it (#193, specs/095). Returns whether
/// anything changed - false for a redelivery or a decision older than the one stored.
/// </summary>
public record RecordSellerSuspensionCommand(Guid SellerId, bool Suspended, DateTime ChangedAt) : IRequest<bool>;

public class RecordSellerSuspensionCommandHandler(
    ISellerRepository sellers,
    IProductRepository products,
    ISavedProductRepository saved,
    INotifier notifier,
    IEmailSender email,
    ILogger<RecordSellerSuspensionCommandHandler> logger) : IRequestHandler<RecordSellerSuspensionCommand, bool>
{
    private readonly ISellerRepository _sellers = sellers;
    private readonly IProductRepository _products = products;
    private readonly ISavedProductRepository _saved = saved;
    private readonly INotifier _notifier = notifier;
    private readonly IEmailSender _email = email;
    private readonly ILogger<RecordSellerSuspensionCommandHandler> _logger = logger;

    public async Task<bool> Handle(RecordSellerSuspensionCommand request, CancellationToken cancellationToken)
    {
        if (!await _sellers.TryRecordSuspensionAsync(request.SellerId, request.Suspended, request.ChangedAt, cancellationToken))
        {
            _logger.LogInformation("Suspension of seller {SellerId} not recorded: a newer decision is already stored.", request.SellerId);
            return false;
        }

        if (request.Suspended)
        {
            _logger.LogInformation("Seller {SellerId}'s shop is closed; their products are off the shelf.", request.SellerId);
            return true;
        }

        // Reopened: each product now back on sale and in stock is, for whoever saved it, available again (specs/091).
        // Through the consumer's outbox, like every other notice, so it commits with the reinstatement.
        var back = await _products.GetOnSaleBySellerAsync(request.SellerId, cancellationToken);
        foreach (var product in back)
        {
            await SavedProductNotices.BackOnSaleAsync(product, _saved, _notifier, _email, cancellationToken);
        }

        _logger.LogInformation("Seller {SellerId}'s shop is open again; {Count} product(s) back on sale.", request.SellerId, back.Count);
        return true;
    }
}
