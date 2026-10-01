using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Commands.UpdateProductDetails;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetMyProducts;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Products.Translations;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// A seller edits the name, description and category of what they listed (specs/124, #240) - nothing wrote those
/// columns before - and an approved product edited by its seller goes back to review, the category included.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ProductDetailsTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;
    private readonly Guid _alice = Guid.CreateVersion7();

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_seller_changes_name_description_and_category_and_an_approved_product_goes_back_to_review()
    {
        var product = await ApprovedAsync();
        var other = await NewCategoryAsync();

        As(_alice, "Seller");
        var edited = await SendAsync(new UpdateProductDetailsCommand(product.Id, "  Canon EOS R50 V  ", "Small and light.", other));

        Assert.Equal("Canon EOS R50 V", edited.Name);
        Assert.Equal("Small and light.", edited.Description);
        Assert.Equal(other, edited.CategoryId);
        Assert.Equal("Pending", edited.ReviewStatus);
        var stored = await StoredAsync(product.Id);
        Assert.Equal(("Canon EOS R50 V", "Small and light.", other, ProductReviewStatus.Pending),
            (stored.Name, stored.Description, stored.CategoryId, stored.ReviewStatus));
        Assert.Single(Audited(product.Id, "ProductDetailsEdited"));
        Assert.Single(Audited(product.Id, "ProductSentForReview"));
    }

    [Fact]
    public async Task A_category_change_alone_sends_an_approved_product_back_to_review()
    {
        var product = await ApprovedAsync();

        As(_alice, "Seller");
        await SendAsync(new UpdateProductDetailsCommand(product.Id, product.Name, product.Description, await NewCategoryAsync()));

        Assert.Equal(ProductReviewStatus.Pending, (await StoredAsync(product.Id)).ReviewStatus);
    }

    [Fact]
    public async Task Saving_the_same_details_changes_nothing_and_sends_nothing_back()
    {
        var product = await ApprovedAsync();

        As(_alice, "Seller");
        await SendAsync(new UpdateProductDetailsCommand(product.Id, product.Name, product.Description, product.CategoryId));

        Assert.Equal(ProductReviewStatus.Approved, (await StoredAsync(product.Id)).ReviewStatus);
        Assert.Empty(Audited(product.Id, "ProductDetailsEdited"));
    }

    [Fact]
    public async Task Another_sellers_product_is_not_found_and_unchanged()
    {
        var product = await ApprovedAsync();

        As(Guid.CreateVersion7(), "Seller");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new UpdateProductDetailsCommand(product.Id, "Mine now", null, product.CategoryId)));

        Assert.Equal(product.Name, (await StoredAsync(product.Id)).Name);
    }

    [Fact]
    public async Task An_administrator_edits_a_sellers_product_without_sending_it_back()
    {
        var product = await ApprovedAsync();

        As(Guid.CreateVersion7(), "Admin");
        await SendAsync(new UpdateProductDetailsCommand(product.Id, "Corrected by staff", null, product.CategoryId));

        var stored = await StoredAsync(product.Id);
        Assert.Equal("Corrected by staff", stored.Name);
        Assert.Equal(ProductReviewStatus.Approved, stored.ReviewStatus);
    }

    [Fact]
    public async Task An_unknown_category_or_an_empty_name_is_refused_and_writes_nothing()
    {
        var product = await ApprovedAsync();

        As(_alice, "Seller");
        var unknown = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new UpdateProductDetailsCommand(product.Id, "New name", null, Guid.CreateVersion7())));
        Assert.Contains(unknown.Errors, e => e.PropertyName == "CategoryId");
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new UpdateProductDetailsCommand(product.Id, "   ", null, product.CategoryId)));

        var stored = await StoredAsync(product.Id);
        Assert.Equal((product.Name, ProductReviewStatus.Approved), (stored.Name, stored.ReviewStatus));
    }

    [Fact]
    public async Task The_lookup_carries_the_original_text_and_each_languages_own_and_a_list_does_not()
    {
        var product = await ApprovedAsync();
        As(_alice, "Seller");
        var before = (await SendAsync(new GetProductByIdQuery(product.Id)))!;
        Assert.Equal(new ProductText(product.Name, "As listed."), before.Original);
        Assert.Empty(before.Translations!);

        await SendAsync(new SetProductTranslationCommand(product.Id, "en", "Canon EOS R50 (English)", null));
        await SendAsync(new SetProductTranslationCommand(product.Id, "vi", "Máy ảnh Canon EOS R50", "Nhỏ gọn."));

        var after = (await SendAsync(new GetProductByIdQuery(product.Id)))!;
        // As stored: no fallback - the English description is null, not the original's.
        Assert.Equal(
            [new ProductTranslationText("en", "Canon EOS R50 (English)", null), new ProductTranslationText("vi", "Máy ảnh Canon EOS R50", "Nhỏ gọn.")],
            after.Translations);
        // Every language has its own text now, and the original is still there to edit.
        Assert.Equal(new ProductText(product.Name, "As listed."), after.Original);

        // A list (here the seller's own - the translation sent it back to review, off the public one) carries neither.
        var listed = (await SendAsync(new GetMyProductsQuery())).Items.Single(p => p.Id == product.Id);
        Assert.Null(listed.Original);
        Assert.Null(listed.Translations);
    }

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private async Task<ProductResponse> ApprovedAsync()
    {
        As(_alice, "Seller");
        var sku = $"DET{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Details {sku}", "As listed.", 18_500_000m, sku, await NewCategoryAsync()));
        As(Guid.CreateVersion7(), "Moderator");
        return await SendAsync(new ApproveProductCommand(product.Id));
    }

    private async Task<Guid> NewCategoryAsync()
    {
        var id = Guid.CreateVersion7();
        await using var scope = _fixture.NewScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        context.Categories.Add(new Category { Id = id, Name = $"Det {id:N}"[..20], Slug = $"det-{id:N}"[..20] });
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<Product> StoredAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }

    private List<AuditEntryRecorded> Audited(Guid productId, string action) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectId == productId.ToString() && e.Action == action).ToList();

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
