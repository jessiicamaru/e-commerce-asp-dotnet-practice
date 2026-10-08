using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Specifications;

/// <summary>A category's specification in one language, after the per-field fallback (specs/159).</summary>
/// <param name="Language">The language <see cref="Name"/> is in; empty when none was asked for.</param>
public record SpecificationResponse(
    Guid Id,
    Guid CategoryId,
    string Code,
    string Name,
    string Kind,
    int Position,
    List<SpecificationOptionResponse> Options,
    string Language = "")
{
    public static SpecificationResponse From(CategorySpecification s, string language = "", string defaultLanguage = "")
    {
        var translation = string.IsNullOrEmpty(language) ? null : s.Translations.FirstOrDefault(t => t.Language == language);
        return new(
            s.Id,
            s.CategoryId,
            s.Code,
            translation?.Name ?? s.Name,
            s.Kind.ToString(),
            s.Position,
            s.Options.OrderBy(o => o.Position).Select(o => SpecificationOptionResponse.From(o, language, defaultLanguage)).ToList(),
            string.IsNullOrEmpty(language) ? string.Empty : translation is not null ? language : defaultLanguage);
    }
}

public record SpecificationOptionResponse(Guid Id, string Code, string Value, string Language = "")
{
    public static SpecificationOptionResponse From(SpecificationOption o, string language = "", string defaultLanguage = "")
    {
        var translation = string.IsNullOrEmpty(language) ? null : o.Translations.FirstOrDefault(t => t.Language == language);
        return new(
            o.Id,
            o.Code,
            translation?.Value ?? o.Value,
            string.IsNullOrEmpty(language) ? string.Empty : translation is not null ? language : defaultLanguage);
    }
}

/// <summary>
/// One line of a product's specifications table: the specification's name and the product's value, in the reader's
/// language - an option translated, a text as written (specs/159 research D2). <c>OptionId</c> and <c>Text</c> are what
/// the seller's form edits.
/// </summary>
public record ProductSpecificationResponse(
    Guid SpecificationId,
    string Name,
    string Kind,
    Guid? OptionId,
    string? Text,
    string Value);

/// <summary>Which specifications apply where (specs/159 research D1).</summary>
public static class Applicable
{
    /// <summary>
    /// The specifications a product filed under <paramref name="categoryId"/> has: its department's, then its own - in
    /// the order each declares them. Empty for a category that does not exist.
    /// </summary>
    public static async Task<List<CategorySpecification>> ToCategoryAsync(
        Guid categoryId, ICategoryRepository categories, ISpecificationRepository specifications,
        CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(categoryId, cancellationToken);
        if (category is null)
        {
            return [];
        }

        Guid[] chain = category.ParentCategoryId is { } department ? [department, category.Id] : [category.Id];
        var found = await specifications.OfCategoriesAsync(chain, cancellationToken);

        return found
            .OrderBy(s => Array.IndexOf(chain, s.CategoryId))
            .ThenBy(s => s.Position)
            .ThenBy(s => s.CreatedAt)
            .ToList();
    }

    /// <summary>The product's values for the specifications that apply, in their order, as a table.</summary>
    public static List<ProductSpecificationResponse> Table(
        IReadOnlyList<CategorySpecification> applicable,
        IReadOnlyList<ProductSpecification> values,
        string language,
        string defaultLanguage)
    {
        var byId = values.ToDictionary(v => v.SpecificationId);
        var table = new List<ProductSpecificationResponse>();

        foreach (var specification in applicable)
        {
            if (!byId.TryGetValue(specification.Id, out var value))
            {
                continue;
            }

            var name = SpecificationResponse.From(specification, language, defaultLanguage).Name;
            if (value.OptionId is { } optionId)
            {
                var option = specification.Options.FirstOrDefault(o => o.Id == optionId);
                if (option is null)
                {
                    continue;
                }

                table.Add(new(specification.Id, name, specification.Kind.ToString(), optionId, null,
                    SpecificationOptionResponse.From(option, language, defaultLanguage).Value));
            }
            else if (value.Text is { } text)
            {
                table.Add(new(specification.Id, name, specification.Kind.ToString(), null, text, text));
            }
        }

        return table;
    }
}
