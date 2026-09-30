using System.Text.Json;
using Ecommerce.Order.Application.MyData;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Tests;

/// <summary>
/// What Order holds about a person, handed to them (#217, specs/111): every table of the model declared; their own
/// orders, returns, voucher uses, vouchers and payouts - and never a sale, which is somebody else's order.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class MyDataTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    [Fact]
    public async Task Every_table_of_the_model_is_declared_exported_withheld_or_not_personal()
    {
        await using var scope = _fixture.NewScope();
        var model = scope.ServiceProvider.GetRequiredService<OrderDbContext>().Model;

        Assert.Empty(OrderPersonalData.Inventory.Problems(model.GetEntityTypes().Select(e => e.GetTableName()!)));
    }

    [Fact]
    public async Task A_person_gets_their_own_rows_in_every_section_and_nobody_else_s()
    {
        var mai = await PersonWithEverythingAsync("Mai");
        var lan = await PersonWithEverythingAsync("Lan");

        var export = await SendAsync(mai.Id, new GetMyDataQuery());

        Assert.Equal("order", export.Service);
        foreach (var section in OrderPersonalData.Inventory.Sections)
            Assert.NotEmpty(export.Sections[section]);

        var json = JsonSerializer.Serialize(export);
        Assert.Contains(mai.Order.ToString(), json);
        Assert.Contains("Mai Street 1", json);
        Assert.Contains("MAI-TRACK", json);
        Assert.Contains("Mai returns it", json);
        Assert.DoesNotContain(lan.Order.ToString(), json);
        Assert.DoesNotContain("Lan Street 1", json);
        Assert.DoesNotContain("LAN-TRACK", json);
        Assert.DoesNotContain("Lan returns it", json);
    }

    /// <summary>Lan bought from Mai's shop: that sale is Lan's order, not Mai's data.</summary>
    [Fact]
    public async Task A_seller_is_not_handed_their_buyers_orders_nor_who_recorded_a_payout()
    {
        var mai = await PersonWithEverythingAsync("Mai");
        var lan = await PersonWithEverythingAsync("Lan", sellerOfLines: mai.Id);

        var json = JsonSerializer.Serialize(await SendAsync(mai.Id, new GetMyDataQuery()));

        Assert.DoesNotContain(lan.Order.ToString(), json);
        Assert.DoesNotContain("Lan Street 1", json);
        Assert.DoesNotContain(Administrator.ToString(), json);
        Assert.Contains("9012", json);   // where the payout went, as the seller's page shows it
    }

    /// <summary>specs/112: the books stay, without the person - and nobody else's rows move.</summary>
    [Fact]
    public async Task A_deleted_account_leaves_its_orders_for_the_books_without_its_name_street_phone_or_words()
    {
        var mai = await PersonWithEverythingAsync("Mai");
        var lan = await PersonWithEverythingAsync("Lan");

        await SendAsync(Guid.Empty, new EraseAccountCommand(mai.Id));
        await SendAsync(Guid.Empty, new EraseAccountCommand(mai.Id));   // a redelivery changes nothing
        var export = await SendAsync(mai.Id, new GetMyDataQuery());

        foreach (var section in OrderPersonalData.Inventory.Erased)
            Assert.Empty(export.Sections[section]);
        foreach (var section in OrderPersonalData.Inventory.Kept.Keys)
            Assert.NotEmpty(export.Sections[section]);
        var json = JsonSerializer.Serialize(export);
        Assert.Contains(mai.Order.ToString(), json);
        Assert.DoesNotContain("Mai Street 1", json);
        Assert.DoesNotContain("\"RecipientName\":\"Mai\"", json);
        Assert.DoesNotContain("Hanoi", json);
        Assert.DoesNotContain("Mai returns it", json);
        Assert.DoesNotContain("\"PaidToHolder\":\"MAI\"", json);
        Assert.Contains("\"Country\":\"VN\"", json);   // the tax charged depends on it
        Assert.Contains("\"Status\":\"Disabled\"", json);

        var lanJson = JsonSerializer.Serialize(await SendAsync(lan.Id, new GetMyDataQuery()));
        Assert.Contains("Lan Street 1", lanJson);
        Assert.Contains("Lan returns it", lanJson);
        Assert.Contains("\"PaidToHolder\":\"LAN\"", lanJson);
    }

    // ------------------------------------------------------------------ helpers

    private static readonly Guid Administrator = Guid.CreateVersion7();

    private async Task<(Guid Id, Guid Order)> PersonWithEverythingAsync(string name, Guid? sellerOfLines = null)
    {
        var id = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var voucherId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Orders.Add(new Domain.Entities.Order
        {
            Id = orderId, UserId = id, TotalAmount = 15m, Status = OrderStatus.Shipped, Currency = "VND", CreatedAt = now, UpdatedAt = now,
            ShipTo = new ShippingAddress { RecipientName = name, Line1 = $"{name} Street 1", City = "Hanoi", PostalCode = "100000", Country = "VN" },
            Items =
            [
                new OrderItem
                {
                    Id = Guid.CreateVersion7(), OrderId = orderId, ProductId = Guid.CreateVersion7(), ProductName = "Desk Lamp",
                    SellerId = sellerOfLines, Quantity = 1, UnitPrice = 15m,
                },
            ],
            Shipments =
            [
                new OrderShipment
                {
                    Id = shipmentId, OrderId = orderId, SellerId = sellerOfLines, Status = ShipmentStatus.Shipped,
                    TrackingReference = $"{name.ToUpperInvariant()}-TRACK", ShippedAt = now, DeliveredAt = now, UpdatedAt = now,
                },
            ],
        });
        db.ParcelReturns.Add(new ParcelReturn
        {
            Id = Guid.CreateVersion7(), OrderId = orderId, ShipmentId = shipmentId, CustomerId = id, Status = ReturnStatus.Requested,
            Reason = $"{name} returns it", RequestedAt = now, UpdatedAt = now,
        });
        db.Vouchers.Add(new Voucher
        {
            Id = voucherId, Code = $"MY{Guid.NewGuid():N}"[..12].ToUpperInvariant(), SellerId = id, Name = $"{name} sale",
            Benefit = VoucherBenefit.Percent, Percent = 10m, StartsAt = now, Status = VoucherStatus.Active, CreatedBy = id,
            CreatedAt = now, UpdatedAt = now,
        });
        db.VoucherCustomerUses.Add(new VoucherCustomerUse { VoucherId = voucherId, CustomerId = id, Uses = 1 });
        db.VoucherRedemptions.Add(new VoucherRedemption
        {
            Id = Guid.CreateVersion7(), VoucherId = voucherId, OrderId = orderId, CustomerId = id, Code = "USED", Name = $"{name} sale",
            SellerId = id, Benefit = VoucherBenefit.Percent, Amount = 1m, Currency = "VND", CreatedAt = now,
        });
        db.Payouts.Add(new Payout
        {
            Id = Guid.CreateVersion7(), SellerId = id, Currency = "VND", Amount = 100m, PartCount = 1, RecordedBy = Administrator,
            CreatedAt = now, PaidToBank = "Vietcombank", PaidToHolder = name.ToUpperInvariant(), PaidToAccountLast4 = "9012",
        });
        await db.SaveChangesAsync();

        return (id, orderId);
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request)
    {
        _fixture.CurrentUser.Id = caller;
        _fixture.CurrentUser.Roles.Clear();
        _fixture.CurrentUser.Roles.Add("Customer");
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(Guid caller, IRequest request)
    {
        _fixture.CurrentUser.Id = caller;
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
