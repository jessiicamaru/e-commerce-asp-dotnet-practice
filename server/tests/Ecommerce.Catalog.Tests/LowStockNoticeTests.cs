using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Notifications;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Catalog's half of a low-stock notice (#200, specs/102): Inventory said a variant crossed its line; the product's seller
/// is told which product and variant, and how many are left - and nobody is told for the shop's own goods.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class LowStockNoticeTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task The_seller_is_told_which_variant_and_how_many_are_left()
    {
        var seller = Guid.CreateVersion7();
        As(seller, "Seller");
        var product = await CreateAsync();
        await using (var scope = _fixture.NewScope())
        {
            // The first variant reuses the product's id (specs/020).
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().ProductVariants.Where(v => v.Id == product.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(v => v.OptionSummary, "Colour: Silver"));
        }

        Assert.True(await SendAsync(new NotifyLowStockCommand(product.Id, 4)));

        var told = Assert.Single(Notices(seller));
        Assert.Equal(
            (NotificationKind.StockRunningLow, $"{product.Name} · Colour: Silver", "4", $"/shop/products/{product.Id}"),
            (told.Kind, told.Data["product"], told.Data["left"], told.Link));
        Assert.Empty(NotificationContract.Problems(told.Kind, told.Data));
    }

    [Fact]
    public async Task Nobody_is_told_about_the_shops_own_goods_or_a_variant_that_is_gone()
    {
        var admin = Guid.CreateVersion7();
        As(admin, "Admin");
        var shops = await CreateAsync();

        Assert.False(await SendAsync(new NotifyLowStockCommand(shops.Id, 2)));
        Assert.False(await SendAsync(new NotifyLowStockCommand(Guid.CreateVersion7(), 2)));
        Assert.DoesNotContain(_fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message),
            n => n.Kind == NotificationKind.StockRunningLow && n.Data.GetValueOrDefault("product", "").StartsWith(shops.Name));
    }

    // ------------------------------------------------------------------ helpers

    private List<UserNotificationRequested> Notices(Guid recipient) =>
        _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.RecipientId == recipient && n.Kind == NotificationKind.StockRunningLow).ToList();

    private void As(Guid id, params string[] roles)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.GivenName = null;
        caller.Roles.Clear();
        foreach (var role in roles)
            caller.Roles.Add(role);
    }

    private async Task<ProductResponse> CreateAsync()
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Ls {categoryId:N}"[..20], Slug = $"ls-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"LOW{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Running low {sku}", null, 1_000_000m, sku, categoryId));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
