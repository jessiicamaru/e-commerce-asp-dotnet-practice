using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Reviews;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Reviews (specs/046): only somebody who received the product writes one, one each, and the average on
/// the product is always the average of the reviews a shopper can see.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ReviewTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;
    private readonly Guid _seller = Guid.CreateVersion7();

    public void Dispose()
    {
        var caller = Caller();
        caller.Id = Guid.CreateVersion7();
        caller.Roles.Clear();
        caller.Roles.Add("Admin");
        caller.GivenName = null;
        caller.Email = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Somebody_who_has_not_received_it_cannot_review_it()
    {
        var product = await ProductAsync();
        var lan = AsCustomer("Lan");

        var refused = await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(new WriteReviewCommand(product.Id, 5, "Great")));
        Assert.Equal(ReviewHandlers.NotEligible, refused.Message);
        Assert.False((await SendAsync(new GetMyReviewQuery(product.Id))).Eligible);

        await ReceivedAsync(lan, product.Id);
        Assert.True((await SendAsync(new GetMyReviewQuery(product.Id))).Eligible);
    }

    [Fact]
    public async Task One_review_each_signed_with_the_first_name_and_the_average_follows()
    {
        var product = await ProductAsync();
        var lan = AsCustomer("Lan");
        await ReceivedAsync(lan, product.Id);
        var first = await SendAsync(new WriteReviewCommand(product.Id, 5, "Sharp and quiet"));
        var edited = await SendAsync(new WriteReviewCommand(product.Id, 4, "Sharp, a little loud"));

        var minh = AsCustomer("Minh");
        await ReceivedAsync(minh, product.Id);
        await SendAsync(new WriteReviewCommand(product.Id, 2, null));

        Assert.Equal(first.Id, edited.Id);                  // a second write is an edit
        Assert.Equal("Lan", edited.AuthorName);
        var read = await ProductReadAsync(product.Id);
        Assert.Equal((3.00m, 2), (read.RatingAverage, read.RatingCount));
        Assert.Equal(2, (await SendAsync(new GetProductReviewsQuery(product.Id))).TotalCount);
    }

    /// <summary>Hidden: gone from the page AND from the average. Restored: back in both.</summary>
    [Fact]
    public async Task A_hidden_review_is_neither_shown_nor_counted()
    {
        var product = await ProductAsync();
        var lan = AsCustomer("Lan");
        await ReceivedAsync(lan, product.Id);
        var spam = await SendAsync(new WriteReviewCommand(product.Id, 1, "Buy from my site instead"));
        var minh = AsCustomer("Minh");
        await ReceivedAsync(minh, product.Id);
        await SendAsync(new WriteReviewCommand(product.Id, 5, null));

        AsStaff();
        await SendAsync(new HideReviewCommand(spam.Id, "Advertising"));
        Assert.Equal((5.00m, 1), await RatingAsync(product.Id));
        Assert.DoesNotContain((await SendAsync(new GetProductReviewsQuery(product.Id))).Items, r => r.Id == spam.Id);
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new HideReviewCommand(spam.Id, "Again")));

        await SendAsync(new RestoreReviewCommand(spam.Id));
        Assert.Equal((3.00m, 2), await RatingAsync(product.Id));
        Assert.Contains(Audited(spam.Id), e => e.Action == "ReviewHidden" && e.Category == "Moderation");
    }

    [Fact]
    public async Task The_seller_is_told_about_a_new_review_not_about_every_edit()
    {
        var product = await ProductAsync();
        var lan = AsCustomer("Lan");
        await ReceivedAsync(lan, product.Id);

        await SendAsync(new WriteReviewCommand(product.Id, 5, null));
        await SendAsync(new WriteReviewCommand(product.Id, 4, null));

        var told = _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.Kind == "NewReview" && n.Data["product"] == product.Name).ToList();
        Assert.Equal((_seller, "5"), (Assert.Single(told).RecipientId, told[0].Data["rating"]));
    }

    [Fact]
    public async Task Receiving_it_twice_is_one_right_to_review()
    {
        var product = await ProductAsync();
        var lan = AsCustomer("Lan");

        await ReceivedAsync(lan, product.Id);
        await ReceivedAsync(lan, product.Id);   // a redelivered event, or a second parcel of the same camera

        await using var scope = _fixture.NewScope();
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .ReviewEligibility.Count(e => e.ProductId == product.Id && e.CustomerId == lan));
    }

    // ------------------------------------------------------------------ helpers

    private TestCaller Caller() => _fixture.Services.GetRequiredService<TestCaller>();

    private Guid AsCustomer(string name)
    {
        var caller = Caller();
        caller.Id = Guid.CreateVersion7();
        caller.Roles.Clear();
        caller.Roles.Add("Customer");
        caller.GivenName = name;
        return caller.Id.Value;
    }

    private void AsStaff()
    {
        var caller = Caller();
        caller.Id = Guid.CreateVersion7();
        caller.Roles.Clear();
        caller.Roles.Add("Moderator");
    }

    /// <summary>A seller's product, approved - what a customer can buy and so receive.</summary>
    private async Task<ProductResponse> ProductAsync()
    {
        var caller = Caller();
        caller.Id = _seller;
        caller.Roles.Clear();
        caller.Roles.Add("Seller");

        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Rv {categoryId:N}"[..20], Slug = $"rv-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"RVW{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Reviewed {sku}", null, 9_000_000m, sku, categoryId));
        AsStaff();
        return await SendAsync(new ApproveProductCommand(product.Id));
    }

    /// <summary>What Order's ParcelDeliveredEvent does, through the command its consumer sends.</summary>
    private Task ReceivedAsync(Guid customer, Guid productId) =>
        SendAsync(new RecordReviewEligibilityCommand(customer, [productId], DateTime.UtcNow));

    private async Task<ProductResponse> ProductReadAsync(Guid id)
    {
        AsStaff();
        return (await SendAsync(new GetProductByIdQuery(id)))!;
    }

    private async Task<(decimal?, int)> RatingAsync(Guid id)
    {
        var read = await ProductReadAsync(id);
        return (read.RatingAverage, read.RatingCount);
    }

    private List<AuditEntryRecorded> Audited(Guid reviewId) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectId == reviewId.ToString()).ToList();

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
