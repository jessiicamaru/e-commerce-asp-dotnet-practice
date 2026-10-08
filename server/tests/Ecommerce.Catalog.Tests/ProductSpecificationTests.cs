using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Catalog.Application.Products.Review;
using Ecommerce.Catalog.Application.Specifications;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Product specifications (specs/159, #366): declared per category, inherited from the department, filled in by the
/// product's seller, read as a table in the reader's language and filtered on - against PostgreSQL.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class ProductSpecificationTests(CatalogTestFixture fixture) : IDisposable
{
    private readonly CatalogTestFixture _fixture = fixture;

    public void Dispose()
    {
        As(Guid.CreateVersion7(), "Admin");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_category_has_its_departments_specifications_first_then_its_own_in_the_readers_language()
    {
        var (department, cameras, brand, sensor, resolution) = await CameraTreeAsync();

        var applicable = await SendAsync(new GetCategorySpecificationsQuery(cameras.Id), "en");

        Assert.Equal([brand.Id, sensor.Id, resolution.Id], applicable.Select(s => s.Id));
        Assert.Equal(("Brand", "en"), (applicable[0].Name, applicable[0].Language));
        Assert.Equal(["Sony", "Canon"], applicable[0].Options.Select(o => o.Value));
        // No English yet: the Vietnamese, and it says so.
        Assert.Equal(("Độ phân giải", "vi"), (applicable[2].Name, applicable[2].Language));
        // The department's own list has only what it declares.
        Assert.Equal([brand.Id], (await SendAsync(new GetCategorySpecificationsQuery(department.Id), "vi")).Select(s => s.Id));
    }

    [Fact]
    public async Task Declaring_is_checked()
    {
        var category = await CategoryAsync("Kiem tra");

        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new CreateSpecificationCommand(category.Id, "colour", "Màu", "Choice", [])));
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new CreateSpecificationCommand(category.Id, "model", "Mẫu", "Text", [new("x", "X")])));
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new CreateSpecificationCommand(category.Id, "Not A Code", "Tên", "Text")));
        await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new CreateSpecificationCommand(category.Id, "size", "Cỡ", "Number")));

        await SendAsync(new CreateSpecificationCommand(category.Id, "model", "Mẫu", "Text"));
        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new CreateSpecificationCommand(category.Id, "model", "Mẫu khác", "Text")));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new CreateSpecificationCommand(Guid.CreateVersion7(), "model", "Mẫu", "Text")));
    }

    [Fact]
    public async Task A_product_shows_its_values_as_a_table_options_translated_text_as_written()
    {
        var (_, cameras, brand, sensor, resolution) = await CameraTreeAsync();
        var product = await ProductAsync(cameras.Id);

        await SendAsync(new SetProductSpecificationsCommand(product.Id,
        [
            new(resolution.Id, Text: "33 MP"),
            new(brand.Id, OptionId: brand.Options[0].Id),
            new(sensor.Id, OptionId: sensor.Options[0].Id),
        ]));

        var english = (await SendAsync(new GetProductByIdQuery(product.Id), "en"))!.Specifications!;
        Assert.Equal(
            [("Brand", "Sony"), ("Sensor", "Full-frame"), ("Độ phân giải", "33 MP")],
            english.Select(s => (s.Name, s.Value)));

        var vietnamese = (await SendAsync(new GetProductByIdQuery(product.Id), "vi"))!.Specifications!;
        Assert.Equal("Cảm biến toàn khung", vietnamese[1].Value);

        Assert.Single(Audited(product.Id, "ProductSpecificationsSet"));
    }

    [Fact]
    public async Task Values_that_do_not_fit_the_products_specifications_are_refused_with_where()
    {
        var (_, cameras, brand, sensor, resolution) = await CameraTreeAsync();
        var elsewhere = await CategoryAsync("Noi khac");
        var other = await SendAsync(new CreateSpecificationCommand(elsewhere.Id, "material", "Chất liệu", "Choice", [new("cotton", "Bông")]));
        var product = await ProductAsync(cameras.Id);

        async Task<string> RefusedAsync(params ProductSpecificationInput[] values)
        {
            var refused = await Assert.ThrowsAsync<ValidationException>(() =>
                SendAsync(new SetProductSpecificationsCommand(product.Id, [.. values])));
            return Assert.Single(refused.Errors).PropertyName;
        }

        // Not this product's category's.
        Assert.Equal("Values[0]", await RefusedAsync(new ProductSpecificationInput(other.Id, OptionId: other.Options[0].Id)));
        // An option of another specification.
        Assert.Equal("Values[1]", await RefusedAsync(
            new ProductSpecificationInput(resolution.Id, Text: "33 MP"), new ProductSpecificationInput(brand.Id, OptionId: sensor.Options[0].Id)));
        // A text where a choice is asked, and an option where a text is.
        Assert.Equal("Values[0]", await RefusedAsync(new ProductSpecificationInput(brand.Id, Text: "Sony")));
        Assert.Equal("Values[0]", await RefusedAsync(new ProductSpecificationInput(resolution.Id, OptionId: brand.Options[0].Id)));
        // Empty, too long, twice.
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new SetProductSpecificationsCommand(product.Id, [new(resolution.Id, Text: "  ")])));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new SetProductSpecificationsCommand(product.Id, [new(resolution.Id, Text: new string('x', 201))])));
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new SetProductSpecificationsCommand(product.Id,
            [new(resolution.Id, Text: "a"), new(resolution.Id, Text: "b")])));
    }

    [Fact]
    public async Task The_set_is_replaced_whole()
    {
        var (_, cameras, brand, _, resolution) = await CameraTreeAsync();
        var product = await ProductAsync(cameras.Id);

        await SendAsync(new SetProductSpecificationsCommand(product.Id, [new(brand.Id, OptionId: brand.Options[0].Id), new(resolution.Id, Text: "33 MP")]));
        await SendAsync(new SetProductSpecificationsCommand(product.Id, [new(brand.Id, OptionId: brand.Options[1].Id)]));

        var table = (await SendAsync(new GetProductByIdQuery(product.Id), "en"))!.Specifications!;
        Assert.Equal([("Brand", "Canon")], table.Select(s => (s.Name, s.Value)));
    }

    [Fact]
    public async Task A_sellers_change_sends_an_approved_product_back_to_review_and_an_unchanged_one_does_not()
    {
        var (_, cameras, brand, _, _) = await CameraTreeAsync();
        var alice = Guid.CreateVersion7();
        As(alice, "Seller");
        var product = await ProductAsync(cameras.Id);
        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new ApproveProductCommand(product.Id));

        As(alice, "Seller");
        await SendAsync(new SetProductSpecificationsCommand(product.Id, [new(brand.Id, OptionId: brand.Options[0].Id)]));
        Assert.Equal("Pending", (await SendAsync(new GetProductByIdQuery(product.Id), "vi"))!.ReviewStatus);

        As(Guid.CreateVersion7(), "Moderator");
        await SendAsync(new ApproveProductCommand(product.Id));
        As(alice, "Seller");
        await SendAsync(new SetProductSpecificationsCommand(product.Id, [new(brand.Id, OptionId: brand.Options[0].Id)]));
        Assert.Equal("Approved", (await SendAsync(new GetProductByIdQuery(product.Id), "vi"))!.ReviewStatus);

        // Another seller's product does not exist for them (specs/027).
        As(Guid.CreateVersion7(), "Seller");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            SendAsync(new SetProductSpecificationsCommand(product.Id, [new(brand.Id, OptionId: brand.Options[1].Id)])));
    }

    [Fact]
    public async Task What_a_product_uses_is_not_deleted_under_it_and_goes_with_the_product()
    {
        var (_, cameras, brand, _, resolution) = await CameraTreeAsync();
        var product = await ProductAsync(cameras.Id);
        await SendAsync(new SetProductSpecificationsCommand(product.Id, [new(brand.Id, OptionId: brand.Options[0].Id), new(resolution.Id, Text: "24 MP")]));

        var used = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new DeleteSpecificationCommand(cameras.Id, resolution.Id)));
        Assert.Contains("1 product", used.Message);
        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new DeleteSpecificationOptionCommand(brand.CategoryId, brand.Id, brand.Options[0].Id)));
        // An option nobody holds goes.
        await SendAsync(new DeleteSpecificationOptionCommand(brand.CategoryId, brand.Id, brand.Options[1].Id));

        await SendAsync(new DeleteProductCommand(product.Id));
        await SendAsync(new DeleteSpecificationCommand(cameras.Id, resolution.Id));
        await SendAsync(new DeleteSpecificationOptionCommand(brand.CategoryId, brand.Id, brand.Options[0].Id));
    }

    [Fact]
    public async Task The_listing_keeps_products_holding_every_option_chosen()
    {
        var (department, cameras, brand, sensor, _) = await CameraTreeAsync();
        var sonyFull = await ProductAsync(cameras.Id);
        var sonyCrop = await ProductAsync(cameras.Id);
        var canonFull = await ProductAsync(cameras.Id);
        await ProductAsync(cameras.Id);
        var sony = brand.Options[0].Id;
        var canon = brand.Options[1].Id;
        var full = sensor.Options[0].Id;
        var crop = sensor.Options[1].Id;
        await SendAsync(new SetProductSpecificationsCommand(sonyFull.Id, [new(brand.Id, OptionId: sony), new(sensor.Id, OptionId: full)]));
        await SendAsync(new SetProductSpecificationsCommand(sonyCrop.Id, [new(brand.Id, OptionId: sony), new(sensor.Id, OptionId: crop)]));
        await SendAsync(new SetProductSpecificationsCommand(canonFull.Id, [new(brand.Id, OptionId: canon), new(sensor.Id, OptionId: full)]));

        Assert.Equal(new[] { sonyFull.Id, sonyCrop.Id }.Order(), (await ListAsync(department.Id, [sony])).Order());
        Assert.Equal([sonyFull.Id], await ListAsync(department.Id, [sony, full]));
        Assert.Equal([canonFull.Id], await ListAsync(cameras.Id, [canon]));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A department with Brand (choice) and a category under it with Sensor (choice) and Resolution (text).</summary>
    private async Task<(CategoryResponse Department, CategoryResponse Cameras, SpecificationResponse Brand,
        SpecificationResponse Sensor, SpecificationResponse Resolution)> CameraTreeAsync()
    {
        var department = await CategoryAsync("May anh");
        var cameras = await CategoryAsync("Mirrorless", department.Id);

        var brand = await SendAsync(new CreateSpecificationCommand(department.Id, "brand", "Thương hiệu", "Choice",
            [new("sony", "Sony"), new("canon", "Canon")]));
        await SendAsync(new TranslateSpecificationCommand(department.Id, brand.Id, "en", "Brand"));

        var sensor = await SendAsync(new CreateSpecificationCommand(cameras.Id, "sensor", "Cảm biến", "Choice",
            [new("full-frame", "Cảm biến toàn khung"), new("aps-c", "APS-C")]));
        await SendAsync(new TranslateSpecificationCommand(cameras.Id, sensor.Id, "en", "Sensor"));
        await SendAsync(new TranslateSpecificationOptionCommand(cameras.Id, sensor.Id, sensor.Options[0].Id, "en", "Full-frame"));

        var resolution = await SendAsync(new CreateSpecificationCommand(cameras.Id, "resolution", "Độ phân giải", "Text"));
        return (department, cameras, brand, sensor, resolution);
    }

    private async Task<CategoryResponse> CategoryAsync(string name, Guid? parent = null)
    {
        var id = Guid.CreateVersion7();
        return await SendAsync(new CreateCategoryCommand($"{name} {id:N}"[..(name.Length + 9)], null, $"spec-{id:N}"[..20], parent));
    }

    private async Task<ProductResponse> ProductAsync(Guid category)
    {
        var sku = $"SPEC{Guid.NewGuid():N}"[..20];
        return await SendAsync(new CreateProductCommand($"Spec {sku}", null, 1_000_000m, sku, category));
    }

    private async Task<List<Guid>> ListAsync(Guid category, List<Guid> options) =>
        (await SendAsync(new GetProductsQuery(CategoryId: category, PageSize: 50, OptionIds: options))).Items.Select(p => p.Id).ToList();

    private void As(Guid id, string role)
    {
        var caller = _fixture.Services.GetRequiredService<TestCaller>();
        caller.Id = id;
        caller.Roles.Clear();
        caller.Roles.Add(role);
    }

    private List<AuditEntryRecorded> Audited(Guid subject, string action) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectId == subject.ToString() && e.Action == action)
            .ToList();

    private async Task<T> SendAsync<T>(IRequest<T> request, string language = "vi")
    {
        await using var scope = _fixture.NewScope(language);
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request)
    {
        await using var scope = _fixture.NewScope("vi");
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
