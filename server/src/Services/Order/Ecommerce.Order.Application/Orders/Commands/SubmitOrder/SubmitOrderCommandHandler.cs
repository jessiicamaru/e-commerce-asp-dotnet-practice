using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MassTransit;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.SubmitOrder;

public class SubmitOrderCommandHandler(
    IOrderRepository orderRepository,
    IPublishEndpoint publishEndpoint,
    ICurrentUser currentUser,
    ICatalogPrices catalogPrices,
    ICartReader cartReader,
    IAddressReader addressReader,
    IShippingOptions shippingOptions,
    ITaxRates taxRates,
    ILogger<SubmitOrderCommandHandler> logger
) : IRequestHandler<SubmitOrderCommand, OrderResponse>
{
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICatalogPrices _catalogPrices = catalogPrices;
    private readonly ICartReader _cartReader = cartReader;
    private readonly IAddressReader _addressReader = addressReader;
    private readonly IShippingOptions _shippingOptions = shippingOptions;
    private readonly ITaxRates _taxRates = taxRates;
    private readonly ILogger<SubmitOrderCommandHandler> _logger = logger;

    public async Task<OrderResponse> Handle(SubmitOrderCommand request, CancellationToken cancellationToken)
    {
        // [Authorize] already rejected anonymous callers; this guards against the endpoint
        // being wired up without it.
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        // Ask the catalogue what these things cost, BEFORE anything is staged.
        //
        // The price and the name used to come from the request body, which is how a product listed
        // at 40,000,000 was bought for 1 (issue #18). Catalog owns both; the customer owns only
        // which product and how many.
        //
        // The position matters as much as the call. This runs before the order is staged, so a
        // refusal leaves no row, no event and no reservation - all-or-nothing by construction.
        // Putting it between the publish and the save would widen the window between staging and
        // commit whenever Catalog is slow, and Principle III is non-negotiable.
        // What is being bought comes from the caller's CART, not from the request (feature 010).
        // Read over gRPC with the caller's own token forwarded, so Cart identifies them itself.
        var cartItems = await _cartReader.GetMyCartAsync(cancellationToken);

        if (cartItems.Count == 0)
        {
            throw new ConflictException("The cart is empty, so there is nothing to order.");
        }

        // Where it goes (feature 011) - read from Identity with the caller's own token, BEFORE anything
        // is staged, like the cart and the prices. Someone else's address id comes back exactly like a
        // missing one, so the refusal below cannot confirm that it exists.
        var address = await _addressReader.GetMyAddressAsync(request.AddressId, cancellationToken);

        if (address is null)
        {
            if (request.AddressId is not null)
            {
                throw new NotFoundException("Delivery address not found.");
            }

            throw new ConflictException(
                "No delivery address was chosen and there is no default. Save an address first.");
        }

        // The validator has already refused an unknown code; this is the same lookup, for the price.
        var shipping = _shippingOptions.Find(request.ShippingOption)
            ?? throw new FluentValidation.ValidationException($"'{request.ShippingOption}' is not a delivery option.");

        var priced = await _catalogPrices.GetPricesAsync(
            cartItems.Select(i => i.ProductId).Distinct().ToList(),
            cancellationToken);

        var byProduct = priced.ToDictionary(p => p.ProductId);

        // "Exists but cannot be sold" is an answer, not a failure, and it is the caller's to act on.
        // 409 rather than 404, so it stays distinguishable from a product that is not there.
        var unsellable = priced.Where(p => !p.Sellable).Select(p => p.Name).ToList();

        if (unsellable.Count > 0)
        {
            throw new ConflictException(
                $"Not currently for sale: {string.Join(", ", unsellable)}.");
        }

        var orderId = Guid.CreateVersion7();

        // What comes back is COPIED onto the line, never referenced. An order line is a record of
        // a transaction: a price change next week must not rewrite what somebody already bought,
        // and the name is copied too so the order still describes itself after the product is
        // renamed or withdrawn.
        var orderItems = cartItems.Select(item => new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = item.ProductId,
            ProductName = byProduct[item.ProductId].Name,
            Quantity = item.Quantity,
            UnitPrice = byProduct[item.ProductId].Price
        }).ToList();

        // The total, in named parts (feature 012): goods + delivery + tax - discount. Tax follows the
        // destination, is computed per line and on delivery and rounded half away from zero (ADR-002).
        // The grand total travels in OrderSubmittedEvent and the saga charges exactly that, so no
        // contract changes. The rate is stored, so a rate changed tomorrow never rewrites this order.
        var taxRate = _taxRates.RateFor(address.Country);
        var totals = OrderTotals.Compute(
            orderItems.Select(i => (i.UnitPrice, i.Quantity)).ToList(), shipping.Price, taxRate);

        for (var i = 0; i < orderItems.Count; i++)
        {
            orderItems[i].TaxAmount = totals.LineTaxes[i];
        }

        var totalAmount = totals.Total;

        var order = new Domain.Entities.Order
        {
            Id = orderId,
            UserId = userId,
            TotalAmount = totalAmount,
            Status = OrderStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = orderItems,

            // Copies, frozen now. Neither an address edit nor a price change later can reach them.
            ShipTo = new ShippingAddress
            {
                RecipientName = address.RecipientName,
                Line1 = address.Line1,
                Line2 = address.Line2,
                City = address.City,
                Region = address.Region,
                PostalCode = address.PostalCode,
                Country = address.Country,
                Phone = address.Phone
            },
            ShippingOptionCode = shipping.Code,
            ShippingOptionName = shipping.Name,
            ShippingPrice = shipping.Price,
            Subtotal = totals.Subtotal,
            TaxTotal = totals.Tax,
            DiscountTotal = totals.Discount,
            TaxRate = taxRate
        };

        // 1. Stage Order Entity in DbContext
        await _orderRepository.AddAsync(order, cancellationToken);

        // 2. Publish Domain Event via MassTransit Outbox (staged in DbContext ChangeTracker)
        var contractItems = orderItems.Select(x => new OrderItemDto(x.ProductId, x.Quantity, x.UnitPrice)).ToList();

        await _publishEndpoint.Publish(new OrderSubmittedEvent(
            order.Id,
            order.UserId,
            order.TotalAmount,
            contractItems,
            order.CreatedAt
        ), cancellationToken);

        // 3. Save BOTH Order entity and OutboxMessage in 1 single atomic DB transaction
        await _orderRepository.SaveChangesAsync(cancellationToken);

        // Where a checkout's trace begins to carry the order id (feature 013). Everything downstream -
        // every consumer and the saga - adds it through OrderIdLogScopeFilter.
        Activity.Current?.SetTag("order.id", order.Id.ToString());
        _logger.LogInformation(
            "Order {OrderId} submitted: {LineCount} line(s), total {TotalAmount} to {Country} by {ShippingOption}",
            order.Id, orderItems.Count, order.TotalAmount, address.Country, shipping.Code);

        var itemResponses = orderItems.Select(x => new OrderItemResponse(
            x.ProductId,
            x.ProductName,
            x.Quantity,
            x.UnitPrice,
            x.TotalPrice,
            x.TaxAmount
        )).ToList();

        return new OrderResponse(
            order.Id,
            order.UserId,
            order.TotalAmount,
            order.Status.ToString(),
            order.CreatedAt,
            itemResponses,
            OrderMapping.ToResponse(order.ShipTo),
            new ShippingOptionResponse(shipping.Code, shipping.Name),
            shipping.Price,
            totals.Subtotal,
            totals.Tax,
            totals.Discount,
            taxRate
        );
    }
}
