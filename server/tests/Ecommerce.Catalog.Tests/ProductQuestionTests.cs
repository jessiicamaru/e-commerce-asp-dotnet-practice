using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Questions;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Questions about a product (specs/076, #110): a customer asks about something on sale, the product's seller - or
/// staff, for the shop's own - answers and nobody else can (a 404, the issue's acceptance), the asker is told once,
/// and staff hide a question or only its answer, which a seller then cannot quietly rewrite.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ProductQuestionTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Asking_needs_a_product_on_sale_and_a_seller_does_not_ask_about_their_own()
    {
        var seller = Guid.CreateVersion7();
        As(seller, "Seller");
        var pending = await CreateAsync();   // a seller's new product waits for review
        var listed = await SellersListedAsync(seller);

        As(Guid.CreateVersion7(), "Customer");
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new AskQuestionCommand(pending.Id, "Charger included?")));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new AskQuestionCommand(Guid.CreateVersion7(), "Anyone?")));

        As(seller, "Seller", "Customer");
        var refused = await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(new AskQuestionCommand(listed.Id, "Mine?")));
        Assert.Equal(QuestionHandlers.OwnProduct, refused.Message);
    }

    [Fact]
    public async Task Asking_tells_the_seller_and_signs_it_with_the_askers_first_name()
    {
        var seller = Guid.CreateVersion7();
        var product = await SellersListedAsync(seller);
        var shops = await ListedAsync();

        AsCustomer(Guid.CreateVersion7(), "Mai");
        var asked = await SendAsync(new AskQuestionCommand(product.Id, "  Does it come with the charger?  "));
        await SendAsync(new AskQuestionCommand(shops.Id, "Is the shop's own one new?"));

        Assert.Equal("Mai", asked.AskerName);
        Assert.Equal("Does it come with the charger?", asked.Body);
        var told = Notices("NewQuestion", product.Name);
        Assert.Equal(seller, Assert.Single(told).RecipientId);
        Assert.Empty(Notices("NewQuestion", shops.Name));   // the shop's own: staff read their queue
    }

    [Fact]
    public async Task Only_the_products_seller_answers_it_and_an_administrator_there_is_a_404()
    {
        var seller = Guid.CreateVersion7();
        var product = await SellersListedAsync(seller);
        var asker = Guid.CreateVersion7();
        AsCustomer(asker, "Mai");
        var question = await SendAsync(new AskQuestionCommand(product.Id, "Charger?"));

        As(Guid.CreateVersion7(), "Seller");
        var other = await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new AnswerQuestionCommand(question.Id, "Yes")));
        As(Guid.CreateVersion7(), "Admin");
        var admin = await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new AnswerQuestionCommand(question.Id, "Yes")));
        As(Guid.CreateVersion7(), "Seller");
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new AnswerQuestionCommand(Guid.CreateVersion7(), "Yes")));
        Assert.All(new[] { other, admin, missing }, e => Assert.Equal(QuestionHandlers.NotFound, e.Message));

        As(seller, "Seller");
        var answered = await SendAsync(new AnswerQuestionCommand(question.Id, "Yes, in the box."));

        Assert.Equal("Yes, in the box.", answered.Answer);
        Assert.Equal(asker, Assert.Single(Notices("QuestionAnswered", product.Name)).RecipientId);
    }

    [Fact]
    public async Task Staff_answer_the_shops_own_and_a_seller_cannot()
    {
        var product = await ListedAsync();
        AsCustomer(Guid.CreateVersion7(), "Bao");
        var question = await SendAsync(new AskQuestionCommand(product.Id, "Warranty?"));

        As(Guid.CreateVersion7(), "Seller");
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new AnswerQuestionCommand(question.Id, "Two years")));

        As(Guid.CreateVersion7(), "Moderator");
        var answered = await SendAsync(new AnswerQuestionCommand(question.Id, "Two years."));
        Assert.Equal("Two years.", answered.Answer);
    }

    [Fact]
    public async Task A_rewrite_replaces_the_answer_and_tells_nobody_again()
    {
        var seller = Guid.CreateVersion7();
        var product = await SellersListedAsync(seller);
        AsCustomer(Guid.CreateVersion7(), "Mai");
        var question = await SendAsync(new AskQuestionCommand(product.Id, "Charger?"));

        As(seller, "Seller");
        await SendAsync(new AnswerQuestionCommand(question.Id, "Yes."));
        await SendAsync(new AnswerQuestionCommand(question.Id, "Yes - a USB-C one."));

        var page = await SendAsync(new GetProductQuestionsQuery(product.Id));
        Assert.Equal("Yes - a USB-C one.", Assert.Single(page.Items).Answer);
        Assert.Single(Notices("QuestionAnswered", product.Name));
    }

    [Fact]
    public async Task Ten_first_answers_at_once_tell_the_asker_once()
    {
        var seller = Guid.CreateVersion7();
        var product = await SellersListedAsync(seller);
        AsCustomer(Guid.CreateVersion7(), "Mai");
        var question = await SendAsync(new AskQuestionCommand(product.Id, "Charger?"));

        As(seller, "Seller");
        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => SendAsync(new AnswerQuestionCommand(question.Id, $"Yes ({i})"))));

        Assert.Single(Notices("QuestionAnswered", product.Name));
        Assert.StartsWith("Yes (", Assert.Single((await SendAsync(new GetProductQuestionsQuery(product.Id))).Items).Answer);
    }

    [Fact]
    public async Task The_list_is_public_newest_first_and_a_hidden_question_leaves_it_until_restored()
    {
        var product = await ListedAsync();
        var asker = Guid.CreateVersion7();
        AsCustomer(asker, "Mai");
        var older = await SendAsync(new AskQuestionCommand(product.Id, "First?"));
        await Task.Delay(5);
        var newer = await SendAsync(new AskQuestionCommand(product.Id, "Second?"));

        Assert.Equal([newer.Id, older.Id], (await SendAsync(new GetProductQuestionsQuery(product.Id))).Items.Select(q => q.Id));

        As(Guid.CreateVersion7(), "Moderator");
        var hidden = await SendAsync(new HideQuestionCommand(older.Id, "  Off topic  "));
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new HideQuestionCommand(older.Id, "Again")));

        Assert.Equal("Off topic", hidden.HiddenReason);
        Assert.Equal([newer.Id], (await SendAsync(new GetProductQuestionsQuery(product.Id))).Items.Select(q => q.Id));
        var told = Assert.Single(Notices("QuestionHidden", product.Name));
        Assert.Equal(asker, told.RecipientId);
        Assert.Equal("Off topic", told.Data["reason"]);
        Assert.Contains((await SendAsync(new GetQuestionsForStaffQuery(Hidden: true, PageSize: 50))).Items, q => q.Id == older.Id);

        await SendAsync(new RestoreQuestionCommand(older.Id));
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new RestoreQuestionCommand(older.Id)));
        Assert.Equal(2, (await SendAsync(new GetProductQuestionsQuery(product.Id))).TotalCount);
    }

    [Fact]
    public async Task A_hidden_answer_reads_as_unanswered_and_cannot_be_rewritten_until_restored()
    {
        var seller = Guid.CreateVersion7();
        var product = await SellersListedAsync(seller);
        AsCustomer(Guid.CreateVersion7(), "Mai");
        var question = await SendAsync(new AskQuestionCommand(product.Id, "Charger?"));
        As(seller, "Seller");
        await SendAsync(new AnswerQuestionCommand(question.Id, "Buy ours at a discount elsewhere."));

        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new HideAnswerCommand(question.Id, "Sends shoppers elsewhere"));

        var shown = Assert.Single((await SendAsync(new GetProductQuestionsQuery(product.Id))).Items);
        Assert.Null(shown.Answer);
        Assert.Null(shown.AnsweredAt);
        Assert.Equal(seller, Assert.Single(Notices("AnswerHidden", product.Name)).RecipientId);

        As(seller, "Seller");
        var locked = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new AnswerQuestionCommand(question.Id, "Same thing again")));
        Assert.Equal(QuestionHandlers.Locked, locked.Message);

        As(Guid.CreateVersion7(), "Admin");
        await SendAsync(new RestoreAnswerCommand(question.Id));
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new RestoreAnswerCommand(question.Id)));
        As(seller, "Seller");
        var rewritten = await SendAsync(new AnswerQuestionCommand(question.Id, "Yes, in the box."));
        Assert.Equal("Yes, in the box.", rewritten.Answer);
    }

    [Fact]
    public async Task A_hidden_question_cannot_be_answered()
    {
        var seller = Guid.CreateVersion7();
        var product = await SellersListedAsync(seller);
        AsCustomer(Guid.CreateVersion7(), "Mai");
        var question = await SendAsync(new AskQuestionCommand(product.Id, "Rude words"));
        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new HideQuestionCommand(question.Id, "Abuse"));

        As(seller, "Seller");
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new AnswerQuestionCommand(question.Id, "Reply")));
        Assert.Empty(Notices("QuestionAnswered", product.Name));
    }

    [Fact]
    public async Task A_sellers_queue_holds_their_own_unanswered_and_staffs_the_shops_own()
    {
        var mine = Guid.CreateVersion7();
        var mineProduct = await SellersListedAsync(mine);
        var theirs = await SellersListedAsync(Guid.CreateVersion7());
        var shops = await ListedAsync();
        AsCustomer(Guid.CreateVersion7(), "Mai");
        var q1 = await SendAsync(new AskQuestionCommand(mineProduct.Id, "One?"));
        var q2 = await SendAsync(new AskQuestionCommand(mineProduct.Id, "Two?"));
        var qTheirs = await SendAsync(new AskQuestionCommand(theirs.Id, "Theirs?"));
        var qShop = await SendAsync(new AskQuestionCommand(shops.Id, "Shop?"));

        As(mine, "Seller");
        await SendAsync(new AnswerQuestionCommand(q1.Id, "Yes."));
        var open = await SendAsync(new GetQuestionsToAnswerQuery(PageSize: 50));
        var done = await SendAsync(new GetQuestionsToAnswerQuery(Answered: true, PageSize: 50));

        Assert.Equal([q2.Id], open.Items.Select(q => q.Id));
        Assert.Equal([q1.Id], done.Items.Select(q => q.Id));
        Assert.Equal(mineProduct.Name, open.Items[0].ProductName);

        As(Guid.CreateVersion7(), "Admin");
        var staff = await SendAsync(new GetQuestionsToAnswerQuery(PageSize: 50));
        Assert.Contains(staff.Items, q => q.Id == qShop.Id);
        Assert.DoesNotContain(staff.Items, q => q.Id == qTheirs.Id || q.Id == q2.Id);
    }

    // ------------------------------------------------------------------ helpers

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

    private async Task<ProductResponse> ListedAsync()
    {
        As(Guid.CreateVersion7(), "Admin");
        return await CreateAsync();
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
            context.Categories.Add(new Category { Id = categoryId, Name = $"Qa {categoryId:N}"[..20], Slug = $"qa-{categoryId:N}"[..20] });
            await context.SaveChangesAsync();
        }

        var sku = $"QNA{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Asked {sku}", null, 1_000_000m, sku, categoryId));
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
