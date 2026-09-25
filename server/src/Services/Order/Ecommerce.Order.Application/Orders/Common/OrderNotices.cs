using System.Globalization;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Notifications;

namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>What a notification about an order needs (specs/042).</summary>
/// <param name="Language">What the order was placed in (specs/021) - and so what its emails are written in (specs/060).</param>
public record OrderNoticeFacts(Guid OrderId, Guid BuyerId, decimal Total, string Currency, List<ParcelFact> Parcels, string Language = "")
{
    /// <summary>Every seller with a parcel on the order - never the shop, which has nobody to tell.</summary>
    public IEnumerable<Guid> Sellers => Parcels.Where(p => p.SellerId is not null).Select(p => p.SellerId!.Value).Distinct();
}

public record ParcelFact(Guid ShipmentId, Guid? SellerId, string? SellerName);

/// <summary>
/// Who is told what when an order moves (specs/042), in one place so every path says the same thing. Each
/// is called inside the transaction that made the change - a repository <c>stage</c>.
/// </summary>
public static class OrderNotices
{
    private static string Money(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);

    private static Dictionary<string, string> About(Guid orderId) => new() { ["orderId"] = orderId.ToString() };

    public static async Task PaidAsync(INotifier notifier, IEmailSender email, OrderNoticeFacts f, CancellationToken ct)
    {
        var paid = new Dictionary<string, string>(About(f.OrderId)) { ["total"] = Money(f.Total), ["currency"] = f.Currency };
        await notifier.NotifyAsync(f.BuyerId, NotificationKind.OrderPaid, paid, $"/orders/{f.OrderId}", ct);

        // The confirmation email (specs/060), in the language the order was placed in - committed with the
        // settlement, and sent by Identity whenever the mail server can take it.
        await email.SendAsync(f.BuyerId, EmailTemplate.OrderPaid, paid, f.Language, ct);

        foreach (var seller in f.Sellers)
        {
            await notifier.NotifyAsync(seller, NotificationKind.NewSale, About(f.OrderId), $"/shop/sales/{f.OrderId}", ct);
        }
    }

    public static Task FailedAsync(INotifier notifier, OrderNoticeFacts f, CancellationToken ct) =>
        notifier.NotifyAsync(f.BuyerId, NotificationKind.OrderFailed, About(f.OrderId), $"/orders/{f.OrderId}", ct);

    public static Task ShippedAsync(INotifier notifier, OrderNoticeFacts f, Guid? sellerId, string? tracking, CancellationToken ct)
    {
        var parcel = f.Parcels.FirstOrDefault(p => p.SellerId == sellerId);
        var data = new Dictionary<string, string>(About(f.OrderId)) { ["tracking"] = tracking ?? string.Empty };
        if (parcel?.SellerName is { } shop)
        {
            data["shop"] = shop;
        }

        return notifier.NotifyAsync(f.BuyerId, NotificationKind.ParcelShipped, data, $"/orders/{f.OrderId}", ct);
    }

    public static async Task CancelledAsync(INotifier notifier, OrderNoticeFacts f, string by, CancellationToken ct)
    {
        await notifier.NotifyAsync(f.BuyerId, NotificationKind.OrderCancelled,
            new Dictionary<string, string>(About(f.OrderId)) { ["by"] = by }, $"/orders/{f.OrderId}", ct);

        foreach (var seller in f.Sellers)
        {
            await notifier.NotifyAsync(seller, NotificationKind.SaleCancelled, About(f.OrderId), $"/shop/sales/{f.OrderId}", ct);
        }
    }

    public static Task ReceivedAsync(INotifier notifier, OrderNoticeFacts f, Guid shipmentId, CancellationToken ct)
    {
        // The shop's own parcel arriving has nobody to tell; a seller's does.
        var seller = f.Parcels.FirstOrDefault(p => p.ShipmentId == shipmentId)?.SellerId;
        return seller is null
            ? Task.CompletedTask
            : notifier.NotifyAsync(seller.Value, NotificationKind.ParcelReceived, About(f.OrderId), $"/shop/sales/{f.OrderId}", ct);
    }

    /// <summary>
    /// The 7-day sweep took a seller's parcel as delivered (specs/059): worded apart from
    /// <see cref="ReceivedAsync"/>, because nobody confirmed it - and it is when their money becomes due.
    /// </summary>
    public static Task AutoDeliveredAsync(INotifier notifier, OrderNoticeFacts f, Guid shipmentId, CancellationToken ct)
    {
        var seller = f.Parcels.FirstOrDefault(p => p.ShipmentId == shipmentId)?.SellerId;
        return seller is null
            ? Task.CompletedTask
            : notifier.NotifyAsync(seller.Value, NotificationKind.ParcelAutoDelivered, About(f.OrderId), $"/shop/sales/{f.OrderId}", ct);
    }

    public static Task PayoutAsync(INotifier notifier, Guid sellerId, decimal amount, string currency, CancellationToken ct) =>
        notifier.NotifyAsync(sellerId, NotificationKind.PayoutRecorded,
            new Dictionary<string, string> { ["amount"] = Money(amount), ["currency"] = currency }, "/shop/payouts", ct);

    /// <summary>Reads the facts and runs <paramref name="send"/>; nothing when the order has vanished.</summary>
    public static async Task WithFactsAsync(
        IOrderRepository orders, Guid orderId, Func<OrderNoticeFacts, Task> send, CancellationToken ct)
    {
        if (await orders.GetNoticeFactsAsync(orderId, ct) is { } facts)
        {
            await send(facts);
        }
    }
}
