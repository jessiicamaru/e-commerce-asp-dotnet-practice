using Ecommerce.Contracts.Activity;
using Ecommerce.Inventory.Application.Reservations.ExpireStock;
using Ecommerce.Inventory.Application.Reservations.ReserveStock;
using Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;
using Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>What Inventory tells the audit log (specs/041): stock set by a person, holds the sweeper expired.</summary>
[Collection(nameof(InventoryTestCollection))]
public class AuditTests(InventoryTestFixture fixture)
{
    private readonly InventoryTestFixture _fixture = fixture;

    [Fact]
    public async Task Setting_stock_records_the_old_and_the_new_count()
    {
        var productId = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(productId, $"SKU-{productId:N}"[..20]));

        await SendAsync(new SetStockOnHandCommand(productId, 7));

        var entry = Assert.Single(Entries(e => e.Action == "StockSet" && e.SubjectId == productId.ToString()));
        Assert.Equal("Catalog", entry.Category);
        Assert.Contains("\"quantityOnHand\":0", entry.Before);
        Assert.Contains("\"quantityOnHand\":7", entry.After);
    }

    /// <summary>The sweeper moves stock nobody asked it to - a System entry, with no actor.</summary>
    [Fact]
    public async Task The_sweeper_records_what_it_returned()
    {
        var productId = Guid.CreateVersion7();
        var orderId = Guid.CreateVersion7();
        await SendAsync(new RegisterProductCommand(productId, $"SKU-{productId:N}"[..20]));
        await SendAsync(new SetStockOnHandCommand(productId, 5));
        await SendAsync(new ReserveStockCommand(orderId, [new ReserveStockItem(productId, 2)]));
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.InventoryDbContext>()
                .StockReservations.Where(r => r.OrderId == orderId)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        await SendAsync(new ExpireStockCommand(BatchSize: 50));

        var entry = Assert.Single(Entries(e => e.Action == "ReservationsExpired" && e.After!.Contains(orderId.ToString())));
        Assert.Equal("System", entry.Category);
        Assert.Null(entry.ActorId);
    }

    private List<AuditEntryRecorded> Entries(Func<AuditEntryRecorded, bool> match) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).Where(match).ToList();

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
