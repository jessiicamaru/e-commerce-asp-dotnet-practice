using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using MediatR;

namespace Ecommerce.Catalog.Application.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler(ICategoryRepository categoryRepository)
    : IRequestHandler<CreateCategoryCommand, CategoryResponse>
{
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    public async Task<CategoryResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var existingCategory = await _categoryRepository.GetBySlugAsync(request.Slug, cancellationToken);

        if (existingCategory != null)
        {
            throw new Exception("Category already exists");
        }

        var category = new Category
        {
            Name = request.Name,
            Description = request.Description,
            Slug = request.Slug,
            ParentCategoryId = request.ParentCategoryId
        };

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return new CategoryResponse
        (
            category.Id,
            category.Name,
            category.Description,
            category.Slug,
            category.ParentCategoryId,
            category.IsActive
        );

    }
}
