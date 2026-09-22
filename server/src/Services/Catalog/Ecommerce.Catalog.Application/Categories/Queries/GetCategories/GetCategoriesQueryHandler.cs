using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Localization;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Categories.Queries.GetCategories;

public class GetCategoriesQueryHandler(
    ICategoryRepository categoryRepository,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization)
    : IRequestHandler<GetCategoriesQuery, List<CategoryResponse>>
{
    private readonly ICategoryRepository _categoryRepository = categoryRepository;
    private readonly IRequestLanguage _language = language;
    private readonly LanguageOptions _localization = localization.Value;

    public async Task<List<CategoryResponse>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);

        return categories
            .Select(c => CategoryResponse.From(c, _language.Current, _localization.DefaultLanguage))
            .ToList();
    }
}
