using Ecommerce.Catalog.Application.Products.Availability;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Application.Products.Variants.UpdateProductVariant;
using Ecommerce.Catalog.Application.Products.Saved;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Saving a product for later (specs/075, #109): the caller's own list, once per product however often or however
/// concurrently it is saved; only something on sale can be saved; what was saved stays in the list and says when it
/// can no longer be bought; and a saved product coming back in stock tells whoever saved it - once per flip.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class SavedProductTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Saving_twice_or_twenty_times_at_once_keeps_one_entry()
    {
        var product = await ListedAsync();
        var mai = Guid.CreateVersion7();
        As(mai, "Customer");

        await SendAsync(new SaveProductCommand(product.Id));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => SendAsync(new SaveProductCommand(product.Id))));

        Assert.Equal([product.Id], await SendAsync(new GetSavedProductIdsQuery()));
    }

    [Fact]
    public async Task Unsaving_removes_it_and_unsaving_what_was_never_saved_is_no_error()
    {
        var product = await ListedAsync();
        As(Guid.CreateVersion7(), "Customer");
        await SendAsync(new SaveProductCommand(product.Id));

        await SendAsync(new UnsaveProductCommand(product.Id));
        await SendAsync(new UnsaveProductCommand(product.Id));
        await SendAsync(new UnsaveProductCommand(Guid.CreateVersion7()));

        Assert.Empty(await SendAsync(new GetSavedProductIdsQuery()));
    }

    /// <summary>The public lookup's rule (specs/045): not on sale is the same 404 as not there.</summary>
    [Fact]
    public async Task Only_a_product_on_sale_can_be_saved()
    {
        var seller = Guid.CreateVersion7();
        As(seller, "Seller");
        var pending = await CreateAsync();   // a seller's new product waits for review
        As(Guid.CreateVersion7(), "Customer");

        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new SaveProductCommand(pending.Id)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new SaveProductCommand(Guid.CreateVersion7())));
        Assert.Empty(await SendAsync(new GetSavedProductIdsQuery()));
    }

    [Fact]
    public async Task The_list_is_the_callers_own_newest_first_in_the_listings_words()
    {
        var older = await ListedAsync();
        var newer = await ListedAsync();
        var mai = Guid.CreateVersion7();
        As(Guid.CreateVersion7(), "Customer");
        await SendAsync(new SaveProductCommand(older.Id));   // somebody else's list
        As(mai, "Customer");
        await SendAsync(new SaveProductCommand(older.Id));
        await Task.Delay(5);
        await SendAsync(new SaveProductCommand(newer.Id));

        var page = await SendAsync(new GetSavedProductsQuery());

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([newer.Id, older.Id], page.Items.Select(i => i.Product.Id));
        Assert.Equal(newer.Name, page.Items[0].Product.Name);
    }

    /// <summary>Research D2: taken down after it was saved, it stays in the list and says it cannot be bought.</summary>
    [Fact]
    public async Task A_product_taken_down_since_stays_and_reads_as_unavailable_and_a_deleted_one_goes()
    {
        var takenDown = await ListedAsync();
        var deleted = await ListedAsync();
        var inStock = await ListedAsync();
        As(Guid.CreateVersion7(), "Customer");
        foreach (var p in new[] { takenDown, deleted, inStock })
            await SendAsync(new SaveProductCommand(p.Id));
        await SendAsync(new RecordStockAvailabilityCommand(inStock.Id, true, DateTime.UtcNow, inStock.Id));

        await using (var scope = _fixture.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Products.Where(p => p.Id == takenDown.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewStatus, ProductReviewStatus.Rejected));
            await db.ProductVariants.Where(v => v.ProductId == deleted.Id).ExecuteDeleteAsync();
            await db.Products.Where(p => p.Id == deleted.Id).ExecuteDeleteAsync();
        }

        var page = await SendAsync(new GetSavedProductsQuery());

        Assert.Equal(2, page.TotalCount);
        Assert.False(page.Items.Single(i => i.Product.Id == takenDown.Id).Available);
        Assert.True(page.Items.Single(i => i.Product.Id == inStock.Id).Available);
    }

    /// <summary>Research D3: none-to-some is news; another "still available" is not.</summary>
    [Fact]
    public async Task Coming_back_in_stock_tells_whoever_saved_it_once_per_flip()
    {
        var product = await ListedAsync();
        var mai = Guid.CreateVersion7();
        var bao = Guid.CreateVersion7();
        As(mai, "Customer");
        await SendAsync(new SaveProductCommand(product.Id));
        As(bao, "Customer");
        await SendAsync(new SaveProductCommand(product.Id));

        var t = DateTime.UtcNow;
        await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, t, product.Id));               // out -> in
        await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, t.AddSeconds(1), product.Id)); // still in
        await SendAsync(new RecordStockAvailabilityCommand(product.Id, false, t.AddSeconds(2), product.Id));// in -> out
        await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, t.AddSeconds(3), product.Id)); // out -> in

        var told = Notices(product.Name);
        Assert.Equal(4, told.Count);   // two shoppers, two flips back in
        Assert.Equal(2, told.Count(n => n.RecipientId == mai));
        Assert.All(told, n => Assert.Equal($"/products/{product.Id}", n.Link));

        // And by email, one per notice (specs/083) - in each saver's language, which only Identity knows.
        var emailed = Emails(product.Id);
        Assert.Equal(4, emailed.Count);
        Assert.Equal(2, emailed.Count(e => e.RecipientId == bao));
        Assert.All(emailed, e => Assert.Equal(
            ("SavedBackInStock", Ecommerce.Shared.Email.EmailTemplate.ReadersLanguage, product.Name),
            (e.Template, e.Language, e.Data["product"])));
    }

    [Fact]
    public async Task A_product_off_the_shelf_coming_back_in_stock_tells_nobody()
    {
        var product = await ListedAsync();
        As(Guid.CreateVersion7(), "Customer");
        await SendAsync(new SaveProductCommand(product.Id));
        await using (var scope = _fixture.NewScope())
        {
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewStatus, ProductReviewStatus.Rejected));
        }

        await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, DateTime.UtcNow, product.Id));

        Assert.Empty(Notices(product.Name));
        Assert.Empty(Emails(product.Id));
    }

    // ---------- #182 (specs/091): back on sale by any route, not only Inventory's ----------

    [Fact]
    public async Task Reactivating_a_variant_in_stock_tells_whoever_saved_it()
    {
        var product = await ListedAsync();
        var mai = Guid.CreateVersion7();
        As(mai, "Customer");
        await SendAsync(new SaveProductCommand(product.Id));
        await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, DateTime.UtcNow, product.Id));   // told once

        As(Guid.CreateVersion7(), "Admin");
        await SendAsync(new UpdateProductVariantCommand(product.Id, product.Id, 1_000_000m, IsActive: false)); // off sale
        await SendAsync(new UpdateProductVariantCommand(product.Id, product.Id, 1_000_000m, IsActive: true));  // back

        Assert.Equal(2, Notices(product.Name).Count(n => n.RecipientId == mai));
        Assert.Equal(2, Emails(product.Id).Count(e => e.RecipientId == mai));
    }

    [Fact]
    public async Task Approving_a_product_in_stock_that_was_off_the_shelf_tells_whoever_saved_it()
    {
        // Sent back to review by a seller's edit, or taken down and resubmitted (specs/045): either way it returns to
        // the shelf by approval, and for somebody who saved it that is the product coming back.
        var product = await ListedAsync();
        var mai = Guid.CreateVersion7();
        As(mai, "Customer");
        await SendAsync(new SaveProductCommand(product.Id));
        await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, DateTime.UtcNow, product.Id));   // told once
        await SetReviewAsync(product.Id, ProductReviewStatus.Pending);

        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new ApproveProductCommand(product.Id));

        var told = Notices(product.Name).Where(n => n.RecipientId == mai).ToList();
        Assert.Equal(2, told.Count);
        Assert.All(told, n => Assert.Equal($"/products/{product.Id}", n.Link));
        Assert.Equal(2, Emails(product.Id).Count(e => e.RecipientId == mai));
    }

    [Fact]
    public async Task Approving_a_product_with_nothing_in_stock_tells_nobody()
    {
        var product = await ListedAsync();
        As(Guid.CreateVersion7(), "Customer");
        await SendAsync(new SaveProductCommand(product.Id));
        await SetReviewAsync(product.Id, ProductReviewStatus.Pending);

        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new ApproveProductCommand(product.Id));

        Assert.Empty(Notices(product.Name));
        Assert.Empty(Emails(product.Id));
    }

    [Fact]
    public async Task An_edit_that_leaves_it_on_sale_tells_nobody_again()
    {
        var product = await ListedAsync();
        var mai = Guid.CreateVersion7();
        As(mai, "Customer");
        await SendAsync(new SaveProductCommand(product.Id));
        await SendAsync(new RecordStockAvailabilityCommand(product.Id, true, DateTime.UtcNow, product.Id));   // told once

        As(Guid.CreateVersion7(), "Admin");
        await SendAsync(new UpdateProductVariantCommand(product.Id, product.Id, 1_200_000m, IsActive: true));
        await SendAsync(new SetVariantPriceCommand(product.Id, product.Id, "VND", 1_100_000m));
        await SendAsync(new AddProductVariantCommand(product.Id, $"ADD{Guid.NewGuid():N}"[..20], 900_000m,
            [new VariantOptionInput("Kit", "Lens")]));

        Assert.Single(Notices(product.Name));
        Assert.Single(Emails(product.Id));
    }

    // ------------------------------------------------------------------ helpers

    private async Task SetReviewAsync(Guid productId, ProductReviewStatus status)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.Where(p => p.Id == productId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewStatus, status));
    }

    private List<Ecommerce.Contracts.Identity.EmailRequested> Emails(Guid productId) =>
        _fixture.Harness.Published.Select<Ecommerce.Contracts.Identity.EmailRequested>().Select(x => x.Context.Message)
            .Where(e => e.Data.GetValueOrDefault("productId") == productId.ToString())
            .ToList();

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

    private async Task<ProductResponse> ListedAsync()
    {
        As(Guid.CreateVersion7(), "Admin");
        return await CreateAsync();
    }

    private async Task<ProductResponse> CreateAsync()
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Sv {categoryId:N}"[..20], Slug = $"sv-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"SAV{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Saved {sku}", null, 1_000_000m, sku, categoryId));
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
}
