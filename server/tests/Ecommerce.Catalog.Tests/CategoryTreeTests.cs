using Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;
using Ecommerce.Catalog.Application.Categories.Commands.DeleteCategory;
using Ecommerce.Catalog.Application.Categories.Commands.MoveCategory;
using Ecommerce.Catalog.Application.Categories.Commands.UpdateCategory;
using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Categories.Queries.GetCategories;
using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Catalog.Tests;

/// <summary>
/// Categories in a tree (specs/158, #363): departments and their categories, two levels, checked on create and on move;
/// a department lists what is under its categories; a department with categories under it is not deleted.
/// </summary>
[Collection(nameof(CatalogTestCollection))]
public class CategoryTreeTests(CatalogTestFixture fixture)
{
    private readonly CatalogTestFixture _fixture = fixture;

    [Fact]
    public async Task A_category_is_created_under_a_department_and_listed_with_its_parent()
    {
        var electronics = await CreateAsync("Dien tu");
        var phones = await CreateAsync("Dien thoai", electronics.Id);

        Assert.Equal(electronics.Id, phones.ParentCategoryId);
        var listed = (await SendAsync(new GetCategoriesQuery())).Single(c => c.Id == phones.Id);
        Assert.Equal(electronics.Id, listed.ParentCategoryId);
    }

    [Fact]
    public async Task A_parent_that_does_not_exist_or_is_not_a_department_is_refused()
    {
        var department = await CreateAsync("Thoi trang");
        var shoes = await CreateAsync("Giay", department.Id);

        var missing = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync("Mu", Guid.CreateVersion7()));
        Assert.Contains(missing.Errors, e => e.PropertyName == "ParentCategoryId");

        // Under "Giày", which is itself under a department: a third level.
        var third = await Assert.ThrowsAsync<ValidationException>(() => CreateAsync("Giay chay", shoes.Id));
        Assert.Contains("two levels", Assert.Single(third.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_category_moves_between_departments_and_to_the_top_and_the_move_is_on_the_record()
    {
        var home = await CreateAsync("Nha cua");
        var kitchen = await CreateAsync("Nha bep");
        var appliances = await CreateAsync("Gia dung", kitchen.Id);

        var moved = await SendAsync(new MoveCategoryCommand(appliances.Id, home.Id));
        Assert.Equal(home.Id, moved.ParentCategoryId);
        Assert.Equal((appliances.Name, appliances.Slug), (moved.Name, moved.Slug));

        var top = await SendAsync(new MoveCategoryCommand(appliances.Id, null));
        Assert.Null(top.ParentCategoryId);

        var entries = Audited(appliances.Id, "CategoryMoved");
        Assert.Equal(2, entries.Count);
        Assert.Contains(kitchen.Id.ToString(), entries[0].Before);
        Assert.Contains(home.Id.ToString(), entries[0].After);
    }

    [Fact]
    public async Task Moving_to_where_it_already_is_changes_and_records_nothing()
    {
        var department = await CreateAsync("Sach");
        var fiction = await CreateAsync("Van hoc", department.Id);

        await SendAsync(new MoveCategoryCommand(fiction.Id, department.Id));

        Assert.Empty(Audited(fiction.Id, "CategoryMoved"));
    }

    [Fact]
    public async Task Moves_that_would_break_the_tree_are_refused()
    {
        var sports = await CreateAsync("The thao");
        var outdoors = await CreateAsync("Da ngoai");
        var tents = await CreateAsync("Leu", outdoors.Id);

        // Itself.
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new MoveCategoryCommand(sports.Id, sports.Id)));
        // Under a category that is under a department.
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new MoveCategoryCommand(sports.Id, tents.Id)));
        // A department with a category under it, moved under another: its category would be a third level.
        var refused = await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new MoveCategoryCommand(outdoors.Id, sports.Id)));
        Assert.Contains("stays a department", Assert.Single(refused.Errors).ErrorMessage);
        // Nothing there.
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new MoveCategoryCommand(Guid.CreateVersion7(), sports.Id)));
    }

    [Fact]
    public async Task A_rename_does_not_move_anything()
    {
        var department = await CreateAsync("May anh");
        var compact = await CreateAsync("Compact", department.Id);

        var renamed = await SendAsync(new UpdateCategoryCommand(compact.Id, "Máy ảnh compact", null));

        Assert.Equal(department.Id, renamed.ParentCategoryId);
    }

    [Fact]
    public async Task A_department_with_categories_under_it_is_a_conflict_not_a_server_error()
    {
        var department = await CreateAsync("Dien tu 2");
        var laptops = await CreateAsync("Laptop", department.Id);

        var refused = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new DeleteCategoryCommand(department.Id)));
        Assert.Contains("1 category under it", refused.Message);

        // Once it is empty, it goes.
        await SendAsync(new DeleteCategoryCommand(laptops.Id));
        await SendAsync(new DeleteCategoryCommand(department.Id));
        Assert.DoesNotContain(await SendAsync(new GetCategoriesQuery()), c => c.Id == department.Id);
    }

    [Fact]
    public async Task A_department_lists_what_is_under_its_categories_and_a_category_only_its_own()
    {
        var department = await CreateAsync("Dien tu 3");
        var phones = await CreateAsync("Dien thoai 3", department.Id);
        var laptops = await CreateAsync("Laptop 3", department.Id);
        var elsewhere = await CreateAsync("Khac");

        var phone = await ProductAsync(phones.Id);
        var laptop = await ProductAsync(laptops.Id);
        var onTheDepartment = await ProductAsync(department.Id);
        await ProductAsync(elsewhere.Id);

        Assert.Equal(
            new[] { phone, laptop, onTheDepartment }.Order(),
            (await ListAsync(department.Id)).Order());
        Assert.Equal([phone], await ListAsync(phones.Id));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<CategoryResponse> CreateAsync(string name, Guid? parent = null)
    {
        var id = Guid.CreateVersion7();
        return await SendAsync(new CreateCategoryCommand($"{name} {id:N}"[..(name.Length + 9)], null, $"tree-{id:N}"[..20], parent));
    }

    private async Task<Guid> ProductAsync(Guid category)
    {
        var sku = $"TREE{Guid.NewGuid():N}"[..20];
        return (await SendAsync(new CreateProductCommand($"Tree {sku}", null, 1_000_000m, sku, category))).Id;
    }

    private async Task<List<Guid>> ListAsync(Guid category) =>
        (await SendAsync(new GetProductsQuery(CategoryId: category, PageSize: 50))).Items.Select(p => p.Id).ToList();

    private List<AuditEntryRecorded> Audited(Guid categoryId, string action) =>
        _fixture.Harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectId == categoryId.ToString() && e.Action == action)
            .OrderBy(e => e.OccurredAt)
            .ToList();

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
