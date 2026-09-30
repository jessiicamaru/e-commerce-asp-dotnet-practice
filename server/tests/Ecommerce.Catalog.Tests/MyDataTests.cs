using System.Text.Json;
using Ecommerce.Catalog.Application.MyData;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// What Catalog holds about a person, handed to them (#217, specs/111): every table of the model declared, every
/// exported section filled from the caller's rows only - never another shopper's, never the staff who acted on them.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class MyDataTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Every_table_of_the_model_is_declared_exported_withheld_or_not_personal()
    {
        await using var scope = _fixture.NewScope();
        var model = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Model;

        Assert.Empty(CatalogPersonalData.Inventory.Problems(model.GetEntityTypes().Select(e => e.GetTableName()!)));
    }

    [Fact]
    public async Task A_person_gets_their_own_rows_in_every_section_and_nobody_else_s()
    {
        var (mai, maiProduct) = await PersonWithEverythingAsync("Mai");
        var (lan, lanProduct) = await PersonWithEverythingAsync("Lan");
        As(mai, "Customer");

        var export = await SendAsync(new GetMyDataQuery());

        Assert.Equal("catalog", export.Service);
        Assert.Contains(export.Withheld, w => w.Table == "product_viewers");
        foreach (var section in CatalogPersonalData.Inventory.Sections)
            Assert.NotEmpty(export.Sections[section]);

        var json = JsonSerializer.Serialize(export);
        Assert.Contains("Mai reviews", json);
        Assert.Contains("Mai asks", json);
        Assert.Contains("Mai reports", json);
        Assert.Contains("Mai Lens", json);
        Assert.Contains(maiProduct.ToString(), json);
        Assert.DoesNotContain("Lan", json);
        Assert.DoesNotContain(lan.ToString(), json);
        Assert.DoesNotContain(lanProduct.ToString(), json);
    }

    [Fact]
    public async Task The_staff_who_hid_or_resolved_something_are_not_named()
    {
        var (mai, _) = await PersonWithEverythingAsync("Mai");
        As(mai, "Customer");

        var json = JsonSerializer.Serialize(await SendAsync(new GetMyDataQuery()));

        Assert.Contains("Hidden for testing", json);   // the reason is theirs to read, as on the page
        Assert.DoesNotContain(Moderator.ToString(), json);
    }

    // ------------------------------------------------------------------ helpers

    private static readonly Guid Moderator = Guid.CreateVersion7();

    /// <summary>A seller and shopper with a row in every exported table; returns their product's id.</summary>
    private async Task<(Guid Id, Guid Product)> PersonWithEverythingAsync(string name)
    {
        var id = Guid.CreateVersion7();
        var categoryId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        await DbAsync(db =>
        {
            db.Categories.Add(new Category { Id = categoryId, Name = $"My {categoryId:N}"[..20], Slug = $"my-{categoryId:N}"[..20] });
            db.Sellers.Add(new Seller { SellerId = id, ShopName = $"{name} Lens", ObservedAt = now });
            return db.SaveChangesAsync();
        });

        As(id, "Seller");
        var sku = $"MYD{Guid.NewGuid():N}"[..20];
        var product = await SendAsync(new CreateProductCommand($"Data {sku}", null, 1_000_000m, sku, categoryId));

        await DbAsync(db =>
        {
            db.Reviews.Add(new Review
            {
                ProductId = product.Id, CustomerId = id, AuthorName = name, Rating = 5, Body = $"{name} reviews",
                HiddenAt = now, HiddenReason = "Hidden for testing", HiddenBy = Moderator,
            });
            db.ReviewEligibility.Add(new ReviewEligibility { ProductId = product.Id, CustomerId = id, FirstDeliveredAt = now });
            db.ProductQuestions.Add(new ProductQuestion { ProductId = product.Id, AskerId = id, AskerName = name, Body = $"{name} asks" });
            db.SavedProducts.Add(new SavedProduct { CustomerId = id, ProductId = product.Id, SavedAt = now });
            db.ContentReports.Add(new ContentReport
            {
                TargetType = ReportTarget.Product, TargetId = product.Id, ProductId = product.Id, ReporterId = id,
                Reason = ReportReason.Other, Details = $"{name} reports", Status = ReportStatus.Dismissed,
                ResolvedAt = now, ResolvedBy = Moderator,
            });
            return db.SaveChangesAsync();
        });

        return (id, product.Id);
    }

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private async Task DbAsync(Func<CatalogDbContext, Task> work)
    {
        await using var scope = _fixture.NewScope();
        await work(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
