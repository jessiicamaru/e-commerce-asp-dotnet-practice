using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Availability;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Saved;
using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Catalog.WebApi.Grpc;
using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Shared.Money;
using Grpc.Core;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A banned seller's shop is closed (#193, specs/095): Identity announces the suspension, Catalog copies it onto the
/// seller's products, and "on the shelf" (specs/092) answers no - for the listing, the lookup and checkout alike.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class SellerSuspensionTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_suspended_sellers_products_leave_the_shelf_everywhere()
    {
        var (seller, product) = await SellerProductOnSaleAsync();

        await SendAsync(new RecordSellerSuspensionCommand(seller, true, DateTime.UtcNow));

        As(Guid.CreateVersion7(), "Customer");
        Assert.Null(await SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.DoesNotContain((await SendAsync(new GetProductsQuery(SearchTerm: product.Sku, PageSize: 50))).Items, p => p.Id == product.Id);
        Assert.False((await PriceAsync(product.Id)).Sellable);

        // Off the shelf like any other: staff still see it.
        As(Guid.CreateVersion7(), "Moderator");
        Assert.NotNull(await SendAsync(new GetProductByIdQuery(product.Id)));
    }

    [Fact]
    public async Task Reinstating_puts_them_back_and_tells_whoever_saved_one_in_stock_once()
    {
        var (seller, product) = await SellerProductOnSaleAsync();
        var mai = Guid.CreateVersion7();
        As(mai, "Customer");
        await SendAsync(new SaveProductCommand(product.Id));
        var t = DateTime.UtcNow;
        await SendAsync(new RecordSellerSuspensionCommand(seller, true, t));

        await SendAsync(new RecordSellerSuspensionCommand(seller, false, t.AddSeconds(1)));
        await SendAsync(new RecordSellerSuspensionCommand(seller, false, t.AddSeconds(1)));   // redelivered

        As(Guid.CreateVersion7(), "Customer");
        Assert.NotNull(await SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.True((await PriceAsync(product.Id)).Sellable);
        Assert.Single(Notices(product.Name), n => n.RecipientId == mai);
    }

    [Fact]
    public async Task An_older_decision_arriving_late_changes_nothing()
    {
        var (seller, product) = await SellerProductOnSaleAsync();
        var t = DateTime.UtcNow;

        await SendAsync(new RecordSellerSuspensionCommand(seller, true, t.AddSeconds(2)));
        var late = await SendAsync(new RecordSellerSuspensionCommand(seller, false, t.AddSeconds(1)));

        Assert.False(late);
        Assert.True(await SuspendedAsync(product.Id));
    }

    [Fact]
    public async Task A_suspension_that_overtakes_the_registration_keeps_the_name_that_follows()
    {
        var seller = Guid.CreateVersion7();
        var t = DateTime.UtcNow;

        await SendAsync(new RecordSellerSuspensionCommand(seller, true, t));
        await SendAsync(new RecordSellerCommand(seller, "Late Lens", t.AddSeconds(-5)));

        await using var scope = _fixture.NewScope();
        var row = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Sellers.AsNoTracking().SingleAsync(s => s.SellerId == seller);
        Assert.Equal(("Late Lens", true), (row.ShopName, row.Suspended));
    }

    [Fact]
    public async Task Another_sellers_products_are_untouched()
    {
        var (_, bystander) = await SellerProductOnSaleAsync();
        var (other, _) = await SellerProductOnSaleAsync();

        await SendAsync(new RecordSellerSuspensionCommand(other, true, DateTime.UtcNow));

        Assert.False(await SuspendedAsync(bystander.Id));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A seller's product, approved and in stock - what a shopper can buy.</summary>
    private async Task<(Guid Seller, ProductResponse Product)> SellerProductOnSaleAsync()
    {
        var seller = Guid.CreateVersion7();
        await SendAsync(new RecordSellerCommand(seller, $"Shop {seller:N}"[..20], DateTime.UtcNow.AddMinutes(-1)));
        As(seller, "Seller");
        var product = await CreateAsync();
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewStatus, ProductReviewStatus.Approved));
        }

        await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, DateTime.UtcNow, product.Id));
        As(Guid.CreateVersion7(), "Admin");
        return (seller, product);
    }

    private async Task<bool> SuspendedAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.AsNoTracking()
            .Where(p => p.Id == productId).Select(p => p.SellerSuspended).SingleAsync();
    }

    private List<UserNotificationRequested> Notices(string productName) =>
        _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.Kind == "SavedBackInStock" && n.Data.TryGetValue("product", out var p) && p == productName)
            .ToList();

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private async Task<ProductResponse> CreateAsync()
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Su {categoryId:N}"[..20], Slug = $"su-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"SUS{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Suspended {sku}", null, 1_000_000m, sku, categoryId));
    }

    private async Task<PricedVariant> PriceAsync(Guid variantId)
    {
        await using var scope = _fixture.NewScope();
        var service = new CatalogPricingService(
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IOptions<CurrencyOptions>>(),
            NullLogger<CatalogPricingService>.Instance,
            scope.ServiceProvider.GetRequiredService<ISellerRepository>());
        var request = new PriceVariantsRequest();
        request.VariantIds.Add(variantId.ToString());
        return (await service.PriceVariants(request, new BareCallContext())).Variants.Single();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private sealed class BareCallContext : ServerCallContext
    {
        protected override string MethodCore => "PriceVariants";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore { get; } = [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore { get; } = [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore { get; } = new(null, []);

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
