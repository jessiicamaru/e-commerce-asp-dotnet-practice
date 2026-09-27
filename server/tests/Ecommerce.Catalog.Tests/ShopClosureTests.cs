using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Availability;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Products.Saved;
using Ecommerce.Catalog.Application.Sellers;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A seller pauses their shop and staff close one (#214, specs/107): three independent reasons - banned, paused, closed -
/// behind the one shelf rule, so lifting one never reopens a shop another keeps shut, and a seller never reopens what
/// staff closed.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ShopClosureTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Pausing_takes_the_shop_off_the_shelf_and_reopening_puts_it_back_telling_savers_once()
    {
        var (seller, product) = await SellerProductOnSaleAsync();
        var mai = Guid.CreateVersion7();
        As(mai, "Customer");
        await SendAsync(new SaveProductCommand(product.Id));

        As(seller, "Seller");
        var paused = await SendAsync(new PauseMyShopCommand());
        Assert.Equal("Paused", paused.State);

        As(Guid.CreateVersion7(), "Customer");
        Assert.Null(await SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.DoesNotContain((await SendAsync(new GetProductsQuery(SearchTerm: product.Sku, PageSize: 50))).Items, p => p.Id == product.Id);
        Assert.Empty(Notices(product.Name));

        As(seller, "Seller");
        Assert.Equal("Open", (await SendAsync(new ResumeMyShopCommand())).State);

        As(Guid.CreateVersion7(), "Customer");
        Assert.NotNull(await SendAsync(new GetProductByIdQuery(product.Id)));
        Assert.Single(Notices(product.Name), n => n.RecipientId == mai);
    }

    [Fact]
    public async Task Pausing_twice_or_reopening_an_open_shop_is_409_and_changes_nothing()
    {
        var (seller, product) = await SellerProductOnSaleAsync();
        As(seller, "Seller");

        var open = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ResumeMyShopCommand()));
        Assert.Equal("The shop is not paused.", open.Message);

        await SendAsync(new PauseMyShopCommand());
        var again = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new PauseMyShopCommand()));
        Assert.Equal("The shop is already paused.", again.Message);
        Assert.True(await OffShelfAsync(product.Id));
    }

    [Fact]
    public async Task Staff_close_with_a_reason_the_seller_reads_and_cannot_undo()
    {
        var (seller, product) = await SellerProductOnSaleAsync();

        As(Guid.CreateVersion7(), "Moderator");
        var closed = await SendAsync(new CloseShopCommand(seller, "  Counterfeit listings  "));
        Assert.Equal(("Closed", "Counterfeit listings"), (closed.State, closed.ClosedReason));
        Assert.True(await OffShelfAsync(product.Id));
        Assert.Contains(_fixture.Harness.Published.Select<UserNotificationRequested>(), x =>
            x.Context.Message.RecipientId == seller && x.Context.Message.Kind == "ShopClosed"
            && x.Context.Message.Data["reason"] == "Counterfeit listings");
        Assert.Contains((await SendAsync(new GetClosedShopsQuery(PageSize: 50))).Items, s => s.SellerId == seller);

        // The seller reads why, and neither pausing nor reopening gets them round it.
        As(seller, "Seller");
        Assert.Equal("Counterfeit listings", (await SendAsync(new GetMyShopQuery())).ClosedReason);
        var resume = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ResumeMyShopCommand()));
        Assert.Equal("Staff closed this shop: Counterfeit listings", resume.Message);
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new PauseMyShopCommand()));
        Assert.True(await OffShelfAsync(product.Id));

        As(Guid.CreateVersion7(), "Admin");
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new CloseShopCommand(seller, "Again")));
        Assert.Equal("Open", (await SendAsync(new ReopenShopCommand(seller))).State);
        Assert.False(await OffShelfAsync(product.Id));
        Assert.Contains(_fixture.Harness.Published.Select<UserNotificationRequested>(), x =>
            x.Context.Message.RecipientId == seller && x.Context.Message.Kind == "ShopReopened");
        Assert.DoesNotContain((await SendAsync(new GetClosedShopsQuery(PageSize: 50))).Items, s => s.SellerId == seller);
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ReopenShopCommand(seller)));
    }

    [Fact]
    public async Task Staff_reopening_keeps_the_sellers_own_pause()
    {
        var (seller, product) = await SellerProductOnSaleAsync();
        As(seller, "Seller");
        await SendAsync(new PauseMyShopCommand());
        As(Guid.CreateVersion7(), "Admin");
        await SendAsync(new CloseShopCommand(seller, "Checking"));
        As(seller, "Seller");
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ResumeMyShopCommand()));

        As(Guid.CreateVersion7(), "Admin");
        var reopened = await SendAsync(new ReopenShopCommand(seller));

        Assert.Equal("Paused", reopened.State);
        Assert.True(await OffShelfAsync(product.Id));
    }

    [Fact]
    public async Task A_lifted_ban_does_not_reopen_a_paused_or_closed_shop()
    {
        var (paused, pausedProduct) = await SellerProductOnSaleAsync();
        As(paused, "Seller");
        await SendAsync(new PauseMyShopCommand());
        var (closed, closedProduct) = await SellerProductOnSaleAsync();
        await SendAsync(new CloseShopCommand(closed, "Checking"));

        var t = DateTime.UtcNow;
        foreach (var seller in new[] { paused, closed })
        {
            await SendAsync(new RecordSellerSuspensionCommand(seller, true, t));
            await SendAsync(new RecordSellerSuspensionCommand(seller, false, t.AddSeconds(1)));
        }

        Assert.True(await OffShelfAsync(pausedProduct.Id));
        Assert.True(await OffShelfAsync(closedProduct.Id));
    }

    [Fact]
    public async Task A_product_approved_while_its_shop_is_paused_stays_off_the_shelf_until_it_reopens()
    {
        var (seller, _) = await SellerProductOnSaleAsync();
        As(seller, "Seller");
        await SendAsync(new PauseMyShopCommand());
        var listed = await CreateAsync();   // pending, as every seller's new product is

        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new ApproveProductCommand(listed.Id));
        Assert.True(await OffShelfAsync(listed.Id));

        As(seller, "Seller");
        await SendAsync(new ResumeMyShopCommand());
        Assert.False(await OffShelfAsync(listed.Id));
    }

    [Fact]
    public async Task The_shop_page_says_a_paused_shop_is_away_and_hides_a_closed_one()
    {
        var (seller, _) = await SellerProductOnSaleAsync();
        As(seller, "Seller");
        await SendAsync(new PauseMyShopCommand());

        As(Guid.CreateVersion7(), "Customer");
        var page = await SendAsync(new GetShopQuery(seller));
        Assert.Equal((true, 0), (page.Paused, page.ProductCount));

        As(Guid.CreateVersion7(), "Admin");
        await SendAsync(new CloseShopCommand(seller, "Checking"));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetShopQuery(seller)));
    }

    [Fact]
    public async Task An_unknown_shop_is_404_and_a_closure_needs_a_reason()
    {
        As(Guid.CreateVersion7(), "Seller");
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new PauseMyShopCommand()));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetMyShopQuery()));

        As(Guid.CreateVersion7(), "Admin");
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new CloseShopCommand(Guid.CreateVersion7(), "Why")));
        var (seller, _) = await SellerProductOnSaleAsync();
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new CloseShopCommand(seller, "  ")));
    }

    [Fact]
    public async Task Each_move_is_audited_staffs_as_moderation()
    {
        var (seller, _) = await SellerProductOnSaleAsync();
        As(seller, "Seller");
        await SendAsync(new PauseMyShopCommand());
        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new CloseShopCommand(seller, "Checking"));

        var entries = _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectType == "Shop" && e.SubjectId == seller.ToString()).ToList();
        Assert.Contains(entries, e => (e.Action, e.Category) == ("ShopPaused", "Catalog"));
        Assert.Contains(entries, e => (e.Action, e.Category) == ("ShopClosed", "Moderation"));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A seller's product, approved and in stock - what a shopper can buy. Leaves the caller an administrator.</summary>
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

    private async Task<bool> OffShelfAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        var product = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.AsNoTracking()
            .SingleAsync(p => p.Id == productId);
        return !product.OnShelf;
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
            context.Categories.Add(new Category { Id = categoryId, Name = $"Cl {categoryId:N}"[..20], Slug = $"cl-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"CLO{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Closure {sku}", null, 1_000_000m, sku, categoryId));
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
