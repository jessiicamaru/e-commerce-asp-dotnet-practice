using System.Text.Json;
using Ecommerce.Payment.Application.MyData;
using Ecommerce.Payment.Domain.Entities;
using Ecommerce.Payment.Domain.Enums;
using Ecommerce.Payment.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// What Payment holds about a person, handed to them (#217, specs/111): their payments and the refunds of them - a
/// refund names nobody, so it is theirs through its payment - and nobody else's.
/// </summary>
[Collection(nameof(PaymentTestCollection))]
public class MyDataTests(PaymentTestFixture fixture) : IDisposable
{
    private readonly PaymentTestFixture _fixture = fixture;

    public void Dispose()
    {
        _fixture.Caller.Id = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Every_table_of_the_model_is_declared_exported_withheld_or_not_personal()
    {
        await using var scope = _fixture.NewScope();
        var model = scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Model;

        Assert.Empty(PaymentPersonalData.Inventory.Problems(model.GetEntityTypes().Select(e => e.GetTableName()!)));
    }

    [Fact]
    public async Task A_person_gets_their_payments_and_refunds_and_nobody_else_s()
    {
        var (mai, maiOrder) = await PaidAndRefundedAsync();
        var (_, lanOrder) = await PaidAndRefundedAsync();
        _fixture.Caller.Id = mai;

        await using var scope = _fixture.NewScope();
        var export = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMyDataQuery());

        Assert.Equal("payment", export.Service);
        Assert.Single(export.Sections["payments"]);
        Assert.Single(export.Sections["refunds"]);
        var json = JsonSerializer.Serialize(export);
        Assert.Contains(maiOrder.ToString(), json);
        Assert.Contains("Stub", json);   // no money moved, and the export keeps saying so
        Assert.DoesNotContain(lanOrder.ToString(), json);
    }

    private async Task<(Guid User, Guid Order)> PaidAndRefundedAsync()
    {
        var user = Guid.CreateVersion7();
        var order = Guid.CreateVersion7();
        var payment = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        db.Payments.Add(new Domain.Entities.Payment
        {
            Id = payment, OrderId = order, UserId = user, Amount = 100m, Currency = "VND", Status = PaymentStatus.Approved,
            Provider = "Stub", ProcessedAt = now,
        });
        db.Refunds.Add(new Refund
        {
            Id = Guid.CreateVersion7(), PaymentId = payment, OrderId = order, Amount = 100m, Currency = "VND", Provider = "Stub", RefundedAt = now,
        });
        await db.SaveChangesAsync();
        return (user, order);
    }
}
