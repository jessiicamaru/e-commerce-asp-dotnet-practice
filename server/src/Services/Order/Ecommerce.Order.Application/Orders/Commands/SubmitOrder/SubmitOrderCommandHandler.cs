using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Ecommerce.Contracts.Order;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Vouchers;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MassTransit;
using MediatR;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Order.Application.Orders.Commands.SubmitOrder;

public class SubmitOrderCommandHandler(
    IOrderRepository orderRepository,
    IPublishEndpoint publishEndpoint,
    ICurrentUser currentUser,
    CheckoutPricing pricing,
    ICommissionRate commission,
    ILogger<SubmitOrderCommandHandler> logger
,
    IAuditTrail audit,
    IVoucherRepository vouchers) : IRequestHandler<SubmitOrderCommand, OrderResponse>
{
    private readonly IVoucherRepository _vouchers = vouchers;
    private readonly IAuditTrail _audit = audit;

    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly CheckoutPricing _pricing = pricing;
    private readonly ICommissionRate _commission = commission;
    private readonly ILogger<SubmitOrderCommandHandler> _logger = logger;

    public async Task<OrderResponse> Handle(SubmitOrderCommand request, CancellationToken cancellationToken)
    {
        // [Authorize] already rejected anonymous callers; this guards against the endpoint
        // being wired up without it.
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        // The cart, the address, the delivery option and Catalog's prices, turned into a total - by the
        // SAME code that answers the checkout quote (#38), so what the customer was shown is what is
        // charged. See CheckoutPricing for where each part comes from and why.
        //
        // The position matters as much as the call. This runs before the order is staged, so a
        // refusal leaves no row, no event and no reservation - all-or-nothing by construction.
        // Putting it between the publish and the save would widen the window between staging and
        // commit whenever another service is slow, and Principle III is non-negotiable.
        var priced = await _pricing.PriceAsync(request.AddressId, request.ShippingOption, cancellationToken, request.VoucherCodes);
        var address = priced.Address;
        var shipping = priced.Shipping;
        var totals = priced.Totals;
        var taxRate = priced.TaxRate;

        var orderId = Guid.CreateVersion7();

        // What comes back is COPIED onto the line, never referenced. An order line is a record of
        // a transaction: a price change next week must not rewrite what somebody already bought,
        // and the name is copied too so the order still describes itself after the product is
        // renamed or withdrawn.
        var orderItems = priced.Lines.Select(line => new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = line.ProductId,
            ProductName = line.Name,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            TaxAmount = line.TaxAmount,

            // Frozen with the name and the price, and for the same reason: a renamed, re-priced or
            // withdrawn variant must not change what this order says was bought (specs/020).
            VariantId = line.VariantId == default ? null : line.VariantId,
            Sku = string.IsNullOrEmpty(line.Sku) ? null : line.Sku,
            OptionSummary = string.IsNullOrEmpty(line.OptionSummary) ? null : line.OptionSummary,

            // ...and whose it was (specs/034). The only record that this sale was a seller's: nothing
            // can recover it later, because the answer lives in Catalog's database, and asking again
            // would answer with whoever owns the product by then.
            SellerId = line.SellerId,

            // ...and the shop's name as it is now (specs/036): who the customer bought from, which a
            // rename next month must not change.
            SellerName = line.SellerName,

            // ...and what vouchers took off it (specs/069), split by who pays: the seller for their own, the
            // shop for the platform's. A refund of the line later is what was actually paid.
            ShopDiscount = line.ShopDiscount,
            PlatformDiscount = line.PlatformDiscount
        }).ToList();

        // ...and what each part earns whoever ships it (specs/037), frozen here with the prices it is
        // computed from. Ordered shop first, then by seller - the order the parcels are listed in, and
        // the first part is the one that takes the remainder of an uneven split (research D2).
        var commissionRate = _commission.Current;
        var sellers = orderItems
            .Select(item => item.SellerId)
            .Distinct()
            .OrderBy(sellerId => sellerId is not null)
            .ThenBy(sellerId => sellerId)
            .ToList();
        var shares = Earnings.SplitDelivery(priced.DeliveryPrice, sellers.Count, priced.Decimals);
        var parts = sellers.Select((sellerId, i) =>
        {
            // A seller's own voucher comes out of their goods (specs/069 research D1) - so their commission is
            // on less and their payout drops; a platform voucher is the shop's cost and changes neither.
            var terms = Earnings.ForPart(
                orderItems.Where(item => item.SellerId == sellerId).Sum(item => item.TotalPrice - item.ShopDiscount),
                commissionRate,
                shares[i],
                isShop: sellerId is null,
                priced.Decimals);

            return new OrderShipment
            {
                Id = Guid.CreateVersion7(),
                OrderId = orderId,
                SellerId = sellerId,
                Status = ShipmentStatus.Pending,
                UpdatedAt = DateTime.UtcNow,
                GoodsTotal = terms.GoodsTotal,
                Commission = terms.Commission,
                ShippingShare = terms.ShippingShare
            };
        }).ToList();

        // The grand total travels in OrderSubmittedEvent and the saga charges exactly that, so no contract
        // changes. The rate is stored, so a rate changed tomorrow never rewrites this order.
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
            // Which language the frozen words above are in (specs/021).
            Language = priced.Language,
            // ...and which currency every amount on this order is in (specs/022).
            Currency = priced.Currency,
            ShippingOptionCode = shipping.Code,
            ShippingOptionName = shipping.Name,
            ShippingPrice = priced.DeliveryPrice,
            Subtotal = totals.Subtotal,
            TaxTotal = totals.Tax,
            DiscountTotal = totals.Discount,
            TaxRate = taxRate,

            // The marketplace's rate now, frozen with everything else (specs/037): a rate changed
            // tomorrow must not change what a seller earned today.
            CommissionRate = commissionRate,

            // One part per seller whose goods are on this order, plus the shop's own (specs/035),
            // saved with the order in the same transaction - so there is never an order whose parts
            // are missing because a second write failed.
            Shipments = parts,

            // The vouchers used, frozen with the order (specs/069): disabling one later changes no order.
            Vouchers = (priced.Vouchers ?? []).Select(v => new VoucherRedemption
            {
                Id = Guid.CreateVersion7(),
                VoucherId = v.VoucherId,
                OrderId = orderId,
                CustomerId = userId,
                Code = v.Code,
                Name = v.Name,
                SellerId = v.SellerId,
                Benefit = v.Benefit,
                Amount = v.Amount,
                Currency = priced.Currency,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };

        // 1. Stage Order Entity in DbContext
        await _orderRepository.AddAsync(order, cancellationToken);

        // 2. Publish Domain Event via MassTransit Outbox (staged in DbContext ChangeTracker)
        // The variant travels with the event: Inventory holds stock per variant (specs/020).
        var contractItems = orderItems
            .Select(x => new OrderItemDto(x.ProductId, x.Quantity, x.UnitPrice, x.VariantId ?? x.ProductId))
            .ToList();

        await _publishEndpoint.Publish(new OrderSubmittedEvent(
            order.Id,
            order.UserId,
            order.TotalAmount,
            contractItems,
            order.CreatedAt,
            // The currency travels WITH the amount, all the way to the row that records the charge
            // (specs/022 research D4). An amount with no currency is what this feature exists to end,
            // and the saga relays both into ProcessPaymentCommand.
            order.Currency ?? string.Empty
        ), cancellationToken);

        // 3. Save BOTH Order entity and OutboxMessage in 1 single atomic DB transaction
        await _audit.RecordAsync(
            AuditCategory.Order, "OrderPlaced", "Order", order.Id.ToString(),
            $"Order placed: {order.TotalAmount} {order.Currency}, {orderItems.Count} line(s)",
            after: new
            {
                order.TotalAmount, order.Currency, order.Subtotal, order.ShippingPrice, order.TaxTotal,
                order.ShippingOptionCode, order.DiscountTotal,
                Vouchers = order.Vouchers.Select(v => new { v.Code, v.Amount }),
                Lines = orderItems.Select(i => new { i.Sku, i.Quantity, i.UnitPrice, i.SellerId, i.ShopDiscount, i.PlatformDiscount })
            },
            cancellationToken: cancellationToken);

        // The order, its outbox message and its audit entry - and a use of every voucher, each claimed by a
        // guarded statement - in ONE transaction (specs/069 research D5). Taking the last use and placing the
        // order cannot come apart: if another checkout took it first, nothing here is saved.
        var usedUp = await _vouchers.ClaimAndSaveAsync(priced.Vouchers ?? [], userId, cancellationToken);
        if (usedUp is not null)
        {
            throw new ConflictException($"Voucher {usedUp} was just used up. Try again without it.");
        }

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
            x.TaxAmount,
            x.VariantId,
            x.Sku,
            x.OptionSummary,
            x.SellerName,
            x.ShopDiscount + x.PlatformDiscount
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
            priced.DeliveryPrice,
            totals.Subtotal,
            totals.Tax,
            totals.Discount,
            taxRate,
            order.Currency ?? string.Empty,
            OrderMapping.ToVouchers(order)
        );
    }
}
