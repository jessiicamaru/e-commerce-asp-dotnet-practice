using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Catalog.Application.Products.Queries.GetMyProducts;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Products.Translations;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Catalog.WebApi.Grpc;
using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Grpc;
using Grpc.Core;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using Ecommerce.Shared.Notifications;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Nothing a seller lists is on the shelf until a moderator has looked (specs/045) - not in the listing,
/// not at its address, and not for sale - and changing what a shopper reads sends it back.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ProductReviewTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;
    private readonly Guid _alice = Guid.CreateVersion7();

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_sellers_new_product_is_hidden_and_unsellable_until_approved()
    {
        var product = await ListAsSellerAsync();
        Assert.Equal("Pending", product.ReviewStatus);

        // A shopper: not listed, not found, not for sale.
        As(Guid.CreateVersion7(), "Customer");
        Assert.DoesNotContain(await SearchAsync(product.Name), p => p.Id == product.Id);
        Assert.Null(await SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.False((await PriceAsync(product.Id)).Sellable);

        // Its seller and the moderators see it.
        As(_alice, "Seller");
        Assert.NotNull(await SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.Contains((await SendAsync(new GetMyProductsQuery())).Items, p => p.Id == product.Id);
        As(Guid.CreateVersion7(), "Moderator");
        Assert.NotNull(await SendAsync(new GetProductByIdQuery(product.Id)));
    }

    [Fact]
    public async Task The_shops_own_product_is_on_sale_as_listed()
    {
        As(Guid.CreateVersion7(), "Admin");
        var product = await CreateAsync();

        Assert.Equal("Approved", product.ReviewStatus);
    }

    [Fact]
    public async Task Approval_puts_it_on_the_shelf_tells_the_seller_and_happens_once()
    {
        var product = await ListAsSellerAsync();
        var moderator = Guid.CreateVersion7();
        As(moderator, "Moderator");

        var approved = await SendAsync(new ApproveProductCommand(product.Id));
        Assert.Equal("Approved", approved.ReviewStatus);
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ApproveProductCommand(product.Id)));

        As(Guid.CreateVersion7(), "Customer");
        Assert.Contains(await SearchAsync(product.Name), p => p.Id == product.Id);
        Assert.True((await PriceAsync(product.Id)).Sellable);

        var told = Notices(product.Name);
        Assert.Equal((_alice, "ProductApproved"), (Assert.Single(told).RecipientId, told[0].Kind));
        Assert.Equal($"/shop/products/{product.Id}", told[0].Link);
        var entry = Assert.Single(Audited(product.Id, "ProductApproved"));
        Assert.Equal((moderator, "Moderation"), (entry.ActorId, entry.Category));
    }

    [Fact]
    public async Task A_rejection_says_why_and_the_seller_can_send_it_back()
    {
        var product = await ListAsSellerAsync();
        As(Guid.CreateVersion7(), "Moderator");
        var rejected = await SendAsync(new RejectProductCommand(product.Id, "Photograph the actual camera"));
        Assert.Equal(("Rejected", "Photograph the actual camera"), (rejected.ReviewStatus, rejected.ReviewReason));
        Assert.Equal("Photograph the actual camera", Notices(product.Name).Single(n => n.Kind == "ProductRejected").Data["reason"]);

        As(_alice, "Seller");
        var resubmitted = await SendAsync(new ResubmitProductCommand(product.Id));
        Assert.Equal("Pending", resubmitted.ReviewStatus);
        Assert.Null(resubmitted.ReviewReason);
    }

    [Fact]
    public async Task Taking_down_an_approved_product_removes_it_from_the_shelf()
    {
        var product = await ApprovedAsync();
        As(Guid.CreateVersion7(), "Moderator");

        await SendAsync(new TakeDownProductCommand(product.Id, "Counterfeit"));
        var told = Notices(product.Name).Single(n => n.Kind == "ProductTakenDown");
        Assert.Equal((_alice, "Counterfeit"), (told.RecipientId, told.Data["reason"]));

        As(Guid.CreateVersion7(), "Customer");
        Assert.Null(await SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.False((await PriceAsync(product.Id)).Sellable);
    }

    /// <summary>Decided with the user: the words and the pictures go back to review; the price does not.</summary>
    [Fact]
    public async Task Editing_the_name_sends_an_approved_product_back_and_a_price_does_not()
    {
        var product = await ApprovedAsync();
        As(_alice, "Seller");

        await SendAsync(new SetVariantPriceCommand(product.Id, product.Id, "VND", 39_000_000m));
        Assert.Equal("Approved", (await SendAsync(new GetProductByIdQuery(product.Id)))!.ReviewStatus);

        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", "A new name", null));
        Assert.Equal("Pending", (await SendAsync(new GetProductByIdQuery(product.Id)))!.ReviewStatus);

        As(Guid.CreateVersion7(), "Customer");
        Assert.Null(await SendAsync(new GetProductByIdQuery(product.Id)));
    }

    [Fact]
    public async Task Staff_see_the_queue_oldest_submission_first()
    {
        var first = await ListAsSellerAsync();
        var second = await ListAsSellerAsync();
        As(Guid.CreateVersion7(), "Moderator");

        var queue = await SendAsync(new GetReviewQueueQuery("Pending", 1, 50));
        var ours = queue.Items.Where(p => p.Id == first.Id || p.Id == second.Id).Select(p => p.Id).ToList();

        Assert.Equal([first.Id, second.Id], ours);
    }

    // ------------------------------------------------------------------ helpers

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private async Task<ProductResponse> ListAsSellerAsync()
    {
        As(_alice, "Seller");
        return await CreateAsync();
    }

    private async Task<ProductResponse> ApprovedAsync()
    {
        var product = await ListAsSellerAsync();
        As(Guid.CreateVersion7(), "Moderator");
        return await SendAsync(new ApproveProductCommand(product.Id));
    }

    private async Task<ProductResponse> CreateAsync()
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Rev {categoryId:N}"[..20], Slug = $"rev-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"REV{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Review {sku}", null, 40_000_000m, sku, categoryId));
    }

    private async Task<List<ProductResponse>> SearchAsync(string name) =>
        (await SendAsync(new GetProductsQuery(1, 50, null, name))).Items;

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

    /// <summary>What a product's seller was told - each checked against what the storefront reads (specs/048).</summary>
    private List<UserNotificationRequested> Notices(string productName)
    {
        var told = _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.Data.TryGetValue("product", out var p) && p == productName).ToList();
        Assert.Empty(told.SelectMany(n => NotificationContract.Problems(n.Kind, n.Data)));
        return told;
    }

    private List<AuditEntryRecorded> Audited(Guid productId, string action) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectId == productId.ToString() && e.Action == action).ToList();

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Just enough of a call for the pricing service to read a cancellation token from.</summary>
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
