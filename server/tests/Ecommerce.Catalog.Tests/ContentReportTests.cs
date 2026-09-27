using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Questions;
using Ecommerce.Catalog.Application.Reports;
using Ecommerce.Catalog.Application.Reviews;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Shoppers report what should not be on the shop (#199, specs/101): a report reaches the moderators' queue; acting on it
/// - through the existing hide or take-down - closes every report of that thing and tells each reporter; so does
/// dismissing it; and one person has one open report per thing.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ContentReportTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_report_reaches_the_queue_and_hiding_the_review_closes_it_and_tells_each_reporter()
    {
        var product = await SellersListedAsync(Guid.CreateVersion7());
        var (author, mai, lan) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var review = await ReviewAsync(author, product.Id, "Buy it cheaper at example.test");

        As(mai, "Customer");
        await SendAsync(new ReportContentCommand("Review", review.Id, "Spam", "  Links to another shop  "));
        As(lan, "Customer");
        await SendAsync(new ReportContentCommand("Review", review.Id, "Misleading", null));

        As(Guid.CreateVersion7(), "Moderator");
        var item = Assert.Single(await QueueAsync(), i => i.TargetId == review.Id);
        Assert.Equal(("Review", product.Id, product.Name, 2), (item.TargetType, item.ProductId, item.ProductName, item.ReportCount));
        Assert.Equal("Buy it cheaper at example.test", item.Excerpt);
        Assert.Equal(new Dictionary<string, int> { ["Spam"] = 1, ["Misleading"] = 1 }, item.Reasons);
        Assert.Equal(["Links to another shop"], item.Details);

        await SendAsync(new HideReviewCommand(review.Id, "Advertising"));

        Assert.DoesNotContain(await QueueAsync(), i => i.TargetId == review.Id);
        var told = Notices(NotificationKind.ReportActioned, product.Name);
        Assert.Equal(new[] { mai, lan }.Order(), told.Select(n => n.RecipientId).Order());
        Assert.Empty(told.SelectMany(n => NotificationContract.Problems(n.Kind, n.Data)));
        Assert.DoesNotContain(told, n => n.Data.Values.Any(v => v.Contains("Moderator")));   // never who decided
        Assert.Equal(2, await CountAsync(review.Id, ReportStatus.Actioned));
    }

    [Fact]
    public async Task A_second_open_report_by_the_same_person_is_refused_and_dismissing_closes_them()
    {
        var product = await SellersListedAsync(Guid.CreateVersion7());
        var mai = Guid.CreateVersion7();

        As(mai, "Customer");
        await SendAsync(new ReportContentCommand("Product", product.Id, "Counterfeit", "The logo is wrong"));
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ReportContentCommand("Product", product.Id, "Spam", null)));

        // Two taps at once are still one report.
        var other = await SellersListedAsync(Guid.CreateVersion7());
        As(mai, "Customer");
        var both = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try { await SendAsync(new ReportContentCommand("Product", other.Id, "Spam", null)); return true; }
            catch (ConflictException) { return false; }
        }));
        Assert.Equal(1, both.Count(ok => ok));

        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new DismissReportsCommand("Product", product.Id));
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new DismissReportsCommand("Product", product.Id)));

        Assert.Equal(mai, Assert.Single(Notices(NotificationKind.ReportDismissed, product.Name)).RecipientId);
        Assert.Contains(_fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message),
            e => e.Action == "ReportsDismissed" && e.SubjectId == product.Id.ToString() && e.Category == "Moderation");
        Assert.True(await OnShelfAsync(product.Id));   // dismissed: left as it is

        // Decided, so the same person may report it again.
        As(mai, "Customer");
        await SendAsync(new ReportContentCommand("Product", product.Id, "Counterfeit", "Still wrong"));
    }

    [Fact]
    public async Task Only_what_a_shopper_can_see_can_be_reported_and_never_ones_own()
    {
        var seller = Guid.CreateVersion7();
        As(seller, "Seller");
        var pending = await CreateAsync();   // waiting for review: not on the shelf
        var listed = await SellersListedAsync(seller);
        var author = Guid.CreateVersion7();
        var review = await ReviewAsync(author, listed.Id, "Fine");
        AsCustomer(Guid.CreateVersion7(), "Mai");
        var question = await SendAsync(new AskQuestionCommand(listed.Id, "Rude words"));
        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new HideQuestionCommand(question.Id, "Abuse"));

        As(Guid.CreateVersion7(), "Customer");
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new ReportContentCommand("Product", pending.Id, "Spam", null)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new ReportContentCommand("Question", question.Id, "Spam", null)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new ReportContentCommand("Review", Guid.CreateVersion7(), "Spam", null)));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new ReportContentCommand("Seller", listed.Id, "Spam", null)));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new ReportContentCommand("Product", listed.Id, "Boring", null)));

        As(seller, "Seller");
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ReportContentCommand("Product", listed.Id, "Spam", null)));
        As(author, "Customer");
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ReportContentCommand("Review", review.Id, "Spam", null)));
    }

    [Fact]
    public async Task Taking_the_product_down_or_hiding_an_answer_closes_their_reports()
    {
        var seller = Guid.CreateVersion7();
        var product = await SellersListedAsync(seller);
        AsCustomer(Guid.CreateVersion7(), "Mai");
        var question = await SendAsync(new AskQuestionCommand(product.Id, "Charger?"));
        As(seller, "Seller");
        await SendAsync(new AnswerQuestionCommand(question.Id, "Buy ours elsewhere."));

        var reporter = Guid.CreateVersion7();
        As(reporter, "Customer");
        await SendAsync(new ReportContentCommand("Question", question.Id, "Misleading", null));
        await SendAsync(new ReportContentCommand("Product", product.Id, "Counterfeit", null));

        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new HideAnswerCommand(question.Id, "Sends shoppers elsewhere"));
        await SendAsync(new TakeDownProductCommand(product.Id, "Counterfeit"));

        Assert.Equal(1, await CountAsync(question.Id, ReportStatus.Actioned));
        Assert.Equal(1, await CountAsync(product.Id, ReportStatus.Actioned));
        Assert.Equal(2, Notices(NotificationKind.ReportActioned, product.Name).Count(n => n.RecipientId == reporter));
    }

    [Fact]
    public async Task The_queue_puts_the_most_reported_first()
    {
        var once = await SellersListedAsync(Guid.CreateVersion7());
        var thrice = await SellersListedAsync(Guid.CreateVersion7());
        As(Guid.CreateVersion7(), "Customer");
        await SendAsync(new ReportContentCommand("Product", once.Id, "Spam", null));
        for (var i = 0; i < 3; i++)
        {
            As(Guid.CreateVersion7(), "Customer");
            await SendAsync(new ReportContentCommand("Product", thrice.Id, "Counterfeit", null));
        }

        As(Guid.CreateVersion7(), "Moderator");
        var ids = (await QueueAsync()).Select(i => i.TargetId).ToList();
        Assert.True(ids.IndexOf(thrice.Id) < ids.IndexOf(once.Id));
        Assert.True(ids.IndexOf(once.Id) >= 0);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<List<ReportedItemResponse>> QueueAsync() =>
        (await SendAsync(new GetReportQueueQuery(1, 100))).Items;

    private async Task<ReviewResponse> ReviewAsync(Guid author, Guid productId, string body)
    {
        await SendAsync(new RecordReviewEligibilityCommand(author, [productId], DateTime.UtcNow));
        AsCustomer(author, "Lan");
        return await SendAsync(new WriteReviewCommand(productId, 1, body));
    }

    private async Task<int> CountAsync(Guid targetId, ReportStatus status)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().ContentReports
            .CountAsync(r => r.TargetId == targetId && r.Status == status);
    }

    private async Task<bool> OnShelfAsync(Guid productId)
    {
        await using var scope = _fixture.NewScope();
        return (await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.SingleAsync(p => p.Id == productId)).OnShelf;
    }

    private List<UserNotificationRequested> Notices(string kind, string productName) =>
        _fixture.Harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.Kind == kind && n.Data.TryGetValue("product", out var p) && p == productName)
            .ToList();

    private void As(Guid id, params string[] roles)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.GivenName = null;
        caller.Roles.Clear();
        foreach (var role in roles)
            caller.Roles.Add(role);
    }

    private void AsCustomer(Guid id, string givenName)
    {
        As(id, "Customer");
        _fixture.Services.GetRequiredService<TestCaller>().GivenName = givenName;
    }

    /// <summary>A seller's product, approved by a moderator - on sale.</summary>
    private async Task<ProductResponse> SellersListedAsync(Guid seller)
    {
        As(seller, "Seller");
        var product = await CreateAsync();
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewStatus, ProductReviewStatus.Approved));
        return product;
    }

    private async Task<ProductResponse> CreateAsync()
    {
        var categoryId = Guid.CreateVersion7();
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Categories.Add(new Category { Id = categoryId, Name = $"Rp {categoryId:N}"[..20], Slug = $"rp-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"RPT{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Reported {sku}", null, 1_000_000m, sku, categoryId));
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
