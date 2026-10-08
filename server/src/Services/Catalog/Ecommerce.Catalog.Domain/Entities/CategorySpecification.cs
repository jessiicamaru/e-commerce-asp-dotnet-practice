namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// Something a category's products are compared by - "Thương hiệu", "Cảm biến", "Chất liệu" - declared once on the
/// category (specs/159, #366). A product has the specifications of its category and of its category's department.
/// </summary>
public class CategorySpecification
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    /// <summary>Unique in its category and fixed once created: what the seed and a link name it by.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The default-language name; other languages are translations, falling back per field (specs/021).</summary>
    public string Name { get; set; } = string.Empty;

    public SpecificationKind Kind { get; set; }

    /// <summary>The order the product page lists them in, within the category.</summary>
    public int Position { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CategorySpecificationTranslation> Translations { get; set; } = [];

    public List<SpecificationOption> Options { get; set; } = [];
}

/// <summary>
/// <b>Text</b> is shown as written and never translated - a model number, a measurement, an author. <b>Choice</b> picks one
/// of the category's options, each translated, and is what the storefront filters on (specs/159 research D2).
/// </summary>
public enum SpecificationKind
{
    Text,
    Choice,
}

public class CategorySpecificationTranslation
{
    public Guid Id { get; set; }

    public Guid SpecificationId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>One value a choice specification offers - "Apple", "Full-frame", "Bông" - translated like any text.</summary>
public class SpecificationOption
{
    public Guid Id { get; set; }

    public Guid SpecificationId { get; set; }

    /// <summary>Unique in its specification and fixed once created.</summary>
    public string Code { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public int Position { get; set; }

    public List<SpecificationOptionTranslation> Translations { get; set; } = [];
}

public class SpecificationOptionTranslation
{
    public Guid Id { get; set; }

    public Guid OptionId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// A product's value for one specification: an option of it (a choice) or a text (specs/159). A product holds at most one
/// per specification; the whole set is replaced when its seller saves it.
/// </summary>
public class ProductSpecification
{
    public Guid ProductId { get; set; }

    public Guid SpecificationId { get; set; }

    public Guid? OptionId { get; set; }

    public string? Text { get; set; }
}
