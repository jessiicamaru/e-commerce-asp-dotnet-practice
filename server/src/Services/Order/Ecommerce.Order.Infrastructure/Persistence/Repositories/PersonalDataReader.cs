using Ecommerce.Order.Application.MyData;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

/// <summary>
/// The person's rows in Order, as handed out (#217, specs/111): what they bought, with its frozen address and each
/// parcel; their returns and voucher uses; and, for a seller, their own vouchers and payouts. ⚠️ A seller's sales are
/// not here - each is another person's order - and neither is the shop's commission on a buyer's parcel.
/// </summary>
public class PersonalDataReader(OrderDbContext context) : IPersonalDataReader
{
    private readonly OrderDbContext _context = context;

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders.AsNoTracking().Where(o => o.UserId == userId).OrderBy(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id, Status = o.Status.ToString(), o.Currency, o.Language, o.TotalAmount, o.Subtotal, o.ShippingPrice, o.TaxTotal,
                o.DiscountTotal, o.ShippingOptionName, o.ShipTo, o.FailureReason, o.CancelledBy, o.CreatedAt, o.PaidAt,
                Lines = o.Items.Select(i => new
                {
                    i.ProductId, i.VariantId, i.ProductName, i.Sku, i.OptionSummary, i.SellerName, i.Quantity, i.UnitPrice,
                    i.TaxAmount, i.ShopDiscount, i.PlatformDiscount,
                }).ToList(),
                Parcels = o.Shipments.Select(s => new
                {
                    s.Id, Status = s.Status.ToString(), s.TrackingReference, s.ShippedAt, s.DeliveredAt, s.DeliveryConfirmedBy,
                    s.CancelledAt, s.CancelReason, s.CancelRefund,
                }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var returns = await _context.ParcelReturns.AsNoTracking().Where(r => r.CustomerId == userId).OrderBy(r => r.RequestedAt)
            .Select(r => new
            {
                r.Id, r.OrderId, r.ShipmentId, Status = r.Status.ToString(), r.Reason, r.DecisionReason, r.TrackingReference,
                r.RequestedAt, r.DecidedAt, r.SentBackAt, r.ReceivedAt, r.RefundAmount,
            })
            .ToListAsync(cancellationToken);

        var voucherUses = await _context.VoucherRedemptions.AsNoTracking().Where(v => v.CustomerId == userId).OrderBy(v => v.CreatedAt)
            .Select(v => new { v.OrderId, v.Code, v.Name, Benefit = v.Benefit.ToString(), v.Amount, v.Currency, v.CreatedAt, v.ReleasedAt })
            .ToListAsync(cancellationToken);

        var useCounts = await _context.VoucherCustomerUses.AsNoTracking().Where(v => v.CustomerId == userId)
            .Select(v => new { v.VoucherId, v.Uses })
            .ToListAsync(cancellationToken);

        var vouchers = await _context.Vouchers.AsNoTracking().Where(v => v.SellerId == userId).OrderBy(v => v.CreatedAt)
            .Select(v => new
            {
                v.Id, v.Code, v.Name, Benefit = v.Benefit.ToString(), v.Percent, Status = v.Status.ToString(), v.StartsAt, v.EndsAt,
                v.TotalLimit, v.PerCustomerLimit, v.UsedCount, v.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // Where the money went, as the seller's own payouts page shows it - not who recorded it.
        var payouts = await _context.Payouts.AsNoTracking().Where(p => p.SellerId == userId).OrderBy(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Currency, p.Amount, p.PartCount, p.CreatedAt, p.PaidToBank, p.PaidToHolder, p.PaidToAccountLast4 })
            .ToListAsync(cancellationToken);

        return new Dictionary<string, IReadOnlyList<object>>
        {
            ["orders"] = orders.Cast<object>().ToList(),
            ["returns"] = returns.Cast<object>().ToList(),
            ["voucherUses"] = voucherUses.Cast<object>().ToList(),
            ["voucherUseCounts"] = useCounts.Cast<object>().ToList(),
            ["vouchers"] = vouchers.Cast<object>().ToList(),
            ["payouts"] = payouts.Cast<object>().ToList(),
        };
    }
}
