using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Translations;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Localization;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Specifications;

// What a category declares (specs/159, #366): administrators create, rename, translate and delete specifications and a
// choice's options. Each write and its audit entry commit in one save. The code of each is fixed once created - it is
// what the seed and a filter link name it by.

public record SpecificationOptionInput(string Code, string Value);

public record CreateSpecificationCommand(
    Guid CategoryId, string Code, string Name, string Kind, List<SpecificationOptionInput>? Options = null)
    : IRequest<SpecificationResponse>;

public record RenameSpecificationCommand(Guid CategoryId, Guid Id, string Name) : IRequest<SpecificationResponse>;

public record TranslateSpecificationCommand(Guid CategoryId, Guid Id, string Language, string Name) : IRequest<SpecificationResponse>;

public record DeleteSpecificationCommand(Guid CategoryId, Guid Id) : IRequest;

public record AddSpecificationOptionCommand(Guid CategoryId, Guid SpecificationId, string Code, string Value)
    : IRequest<SpecificationResponse>;

public record RenameSpecificationOptionCommand(Guid CategoryId, Guid SpecificationId, Guid OptionId, string Value)
    : IRequest<SpecificationResponse>;

public record TranslateSpecificationOptionCommand(Guid CategoryId, Guid SpecificationId, Guid OptionId, string Language, string Value)
    : IRequest<SpecificationResponse>;

public record DeleteSpecificationOptionCommand(Guid CategoryId, Guid SpecificationId, Guid OptionId) : IRequest;

/// <summary>What applies to products filed under a category - its department's, then its own - for anyone.</summary>
public record GetCategorySpecificationsQuery(Guid CategoryId) : IRequest<List<SpecificationResponse>>;

internal static class SpecificationRules
{
    /// <summary>Lower-case words joined by hyphens: <c>brand</c>, <c>screen-size</c>.</summary>
    public static IRuleBuilderOptions<T, string> MustBeACode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(60)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$").WithMessage("A code is lower-case letters and digits joined by hyphens.");
}

public class CreateSpecificationCommandValidator : AbstractValidator<CreateSpecificationCommand>
{
    public CreateSpecificationCommandValidator()
    {
        RuleFor(x => x.Code).MustBeACode();
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("A specification needs a name.").MaximumLength(100);
        RuleFor(x => x.Kind).Must(k => Enum.TryParse<SpecificationKind>(k, ignoreCase: false, out _))
            .WithMessage("Kind is Text or Choice.");
        RuleFor(x => x.Options).Must(o => o is { Count: > 0 }).When(x => x.Kind == nameof(SpecificationKind.Choice))
            .WithMessage("A choice needs at least one option.");
        RuleFor(x => x.Options).Must(o => o is null || o.Count == 0).When(x => x.Kind == nameof(SpecificationKind.Text))
            .WithMessage("A text specification has no options.");
        RuleFor(x => x.Options).Must(o => o is null || o.Select(i => i.Code).Distinct().Count() == o.Count)
            .WithMessage("Two options share a code.");
        RuleForEach(x => x.Options).ChildRules(option =>
        {
            option.RuleFor(o => o.Code).MustBeACode();
            option.RuleFor(o => o.Value).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("An option needs a value.").MaximumLength(100);
        });
    }
}

public class RenameSpecificationCommandValidator : AbstractValidator<RenameSpecificationCommand>
{
    public RenameSpecificationCommandValidator() =>
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("A specification needs a name.").MaximumLength(100);
}

public class TranslateSpecificationCommandValidator : AbstractValidator<TranslateSpecificationCommand>
{
    public TranslateSpecificationCommandValidator(IOptions<LanguageOptions> localization)
    {
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("A translation needs a name.").MaximumLength(100);
        RuleFor(x => x.Language).MustBeSupported(localization.Value);
    }
}

public class AddSpecificationOptionCommandValidator : AbstractValidator<AddSpecificationOptionCommand>
{
    public AddSpecificationOptionCommandValidator()
    {
        RuleFor(x => x.Code).MustBeACode();
        RuleFor(x => x.Value).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("An option needs a value.").MaximumLength(100);
    }
}

public class RenameSpecificationOptionCommandValidator : AbstractValidator<RenameSpecificationOptionCommand>
{
    public RenameSpecificationOptionCommandValidator() =>
        RuleFor(x => x.Value).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("An option needs a value.").MaximumLength(100);
}

public class TranslateSpecificationOptionCommandValidator : AbstractValidator<TranslateSpecificationOptionCommand>
{
    public TranslateSpecificationOptionCommandValidator(IOptions<LanguageOptions> localization)
    {
        RuleFor(x => x.Value).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("A translation needs a value.").MaximumLength(100);
        RuleFor(x => x.Language).MustBeSupported(localization.Value);
    }
}

public class SpecificationCommandHandlers(
    ICategoryRepository categories,
    ISpecificationRepository specifications,
    IAuditTrail audit,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization)
    : IRequestHandler<CreateSpecificationCommand, SpecificationResponse>,
      IRequestHandler<RenameSpecificationCommand, SpecificationResponse>,
      IRequestHandler<TranslateSpecificationCommand, SpecificationResponse>,
      IRequestHandler<DeleteSpecificationCommand>,
      IRequestHandler<AddSpecificationOptionCommand, SpecificationResponse>,
      IRequestHandler<RenameSpecificationOptionCommand, SpecificationResponse>,
      IRequestHandler<TranslateSpecificationOptionCommand, SpecificationResponse>,
      IRequestHandler<DeleteSpecificationOptionCommand>,
      IRequestHandler<GetCategorySpecificationsQuery, List<SpecificationResponse>>
{
    private readonly ICategoryRepository _categories = categories;
    private readonly ISpecificationRepository _specifications = specifications;
    private readonly IAuditTrail _audit = audit;
    private readonly IRequestLanguage _language = language;
    private readonly LanguageOptions _localization = localization.Value;

    private SpecificationResponse Read(CategorySpecification s) =>
        SpecificationResponse.From(s, _language.Current, _localization.DefaultLanguage);

    private async Task<CategorySpecification> FindAsync(Guid categoryId, Guid id, CancellationToken cancellationToken) =>
        await _specifications.GetAsync(categoryId, id, cancellationToken)
        ?? throw new NotFoundException("Specification not found.");

    private static SpecificationOption FindOption(CategorySpecification s, Guid optionId) =>
        s.Options.FirstOrDefault(o => o.Id == optionId) ?? throw new NotFoundException("Option not found.");

    private Task RecordAsync(string action, CategorySpecification s, string summary, object? before, object? after,
        CancellationToken cancellationToken) =>
        _audit.RecordAsync(AuditCategory.Catalog, action, "CategorySpecification", s.Id.ToString(), summary, before, after,
            cancellationToken: cancellationToken);

    public async Task<SpecificationResponse> Handle(CreateSpecificationCommand request, CancellationToken cancellationToken)
    {
        if (await _categories.GetByIdAsync(request.CategoryId, cancellationToken) is null)
            throw new NotFoundException($"Category with ID '{request.CategoryId}' was not found.");

        if (await _specifications.CodeTakenAsync(request.CategoryId, request.Code, cancellationToken))
            throw new ConflictException($"This category already has a specification '{request.Code}'.");

        var position = (await _specifications.OfCategoriesAsync([request.CategoryId], cancellationToken)).Count;
        var specification = new CategorySpecification
        {
            Id = Guid.CreateVersion7(),
            CategoryId = request.CategoryId,
            Code = request.Code,
            Name = request.Name.Trim(),
            Kind = Enum.Parse<SpecificationKind>(request.Kind),
            Position = position,
            Options = (request.Options ?? []).Select((o, i) => new SpecificationOption
            {
                Id = Guid.CreateVersion7(), Code = o.Code, Value = o.Value.Trim(), Position = i,
            }).ToList(),
        };
        foreach (var option in specification.Options) option.SpecificationId = specification.Id;

        _specifications.Add(specification);
        await RecordAsync("SpecificationCreated", specification, $"Specification \"{specification.Name}\" added to a category",
            null, new { specification.Code, specification.Name, Kind = specification.Kind.ToString(),
                Options = specification.Options.Select(o => o.Value) }, cancellationToken);
        await _specifications.SaveChangesAsync(cancellationToken);
        return Read(specification);
    }

    public async Task<SpecificationResponse> Handle(RenameSpecificationCommand request, CancellationToken cancellationToken)
    {
        var specification = await FindAsync(request.CategoryId, request.Id, cancellationToken);
        var before = new { specification.Name };
        specification.Name = request.Name.Trim();
        await RecordAsync("SpecificationRenamed", specification, $"Specification \"{specification.Name}\" renamed",
            before, new { specification.Name }, cancellationToken);
        await _specifications.SaveChangesAsync(cancellationToken);
        return Read(specification);
    }

    public async Task<SpecificationResponse> Handle(TranslateSpecificationCommand request, CancellationToken cancellationToken)
    {
        var specification = await FindAsync(request.CategoryId, request.Id, cancellationToken);
        var lang = request.Language.ToLowerInvariant();
        var translation = specification.Translations.FirstOrDefault(t => t.Language == lang);
        var before = translation is null ? null : new { translation.Name };

        if (translation is null)
            specification.Translations.Add(new() { Id = Guid.CreateVersion7(), SpecificationId = specification.Id, Language = lang, Name = request.Name.Trim() });
        else
            translation.Name = request.Name.Trim();

        await RecordAsync("SpecificationTranslated", specification, $"Specification \"{specification.Name}\" translated into {lang}",
            before, new { Name = request.Name.Trim() }, cancellationToken);
        await _specifications.SaveChangesAsync(cancellationToken);
        return Read(specification);
    }

    public async Task Handle(DeleteSpecificationCommand request, CancellationToken cancellationToken)
    {
        var specification = await FindAsync(request.CategoryId, request.Id, cancellationToken);
        var used = await _specifications.ProductsUsingAsync(specification.Id, cancellationToken);
        if (used > 0)
            throw new ConflictException($"{used} product(s) have a value for \"{specification.Name}\". Clear them first.");

        _specifications.Remove(specification);
        await RecordAsync("SpecificationDeleted", specification, $"Specification \"{specification.Name}\" deleted",
            new { specification.Code, specification.Name }, null, cancellationToken);
        await _specifications.SaveChangesAsync(cancellationToken);
    }

    public async Task<SpecificationResponse> Handle(AddSpecificationOptionCommand request, CancellationToken cancellationToken)
    {
        var specification = await FindAsync(request.CategoryId, request.SpecificationId, cancellationToken);
        if (specification.Kind != SpecificationKind.Choice)
            throw new ValidationException([new ValidationFailure("SpecificationId", "A text specification has no options.")]);
        if (specification.Options.Any(o => o.Code == request.Code))
            throw new ConflictException($"\"{specification.Name}\" already has an option '{request.Code}'.");

        var option = new SpecificationOption
        {
            Id = Guid.CreateVersion7(), SpecificationId = specification.Id, Code = request.Code, Value = request.Value.Trim(),
            Position = specification.Options.Count == 0 ? 0 : specification.Options.Max(o => o.Position) + 1,
        };
        specification.Options.Add(option);
        await RecordAsync("SpecificationOptionAdded", specification, $"Option \"{option.Value}\" added to \"{specification.Name}\"",
            null, new { option.Code, option.Value }, cancellationToken);
        await _specifications.SaveChangesAsync(cancellationToken);
        return Read(specification);
    }

    public async Task<SpecificationResponse> Handle(RenameSpecificationOptionCommand request, CancellationToken cancellationToken)
    {
        var specification = await FindAsync(request.CategoryId, request.SpecificationId, cancellationToken);
        var option = FindOption(specification, request.OptionId);
        var before = new { option.Value };
        option.Value = request.Value.Trim();
        await RecordAsync("SpecificationOptionRenamed", specification, $"An option of \"{specification.Name}\" renamed",
            before, new { option.Value }, cancellationToken);
        await _specifications.SaveChangesAsync(cancellationToken);
        return Read(specification);
    }

    public async Task<SpecificationResponse> Handle(TranslateSpecificationOptionCommand request, CancellationToken cancellationToken)
    {
        var specification = await FindAsync(request.CategoryId, request.SpecificationId, cancellationToken);
        var option = FindOption(specification, request.OptionId);
        var lang = request.Language.ToLowerInvariant();
        var translation = option.Translations.FirstOrDefault(t => t.Language == lang);
        var before = translation is null ? null : new { translation.Value };

        if (translation is null)
            option.Translations.Add(new() { Id = Guid.CreateVersion7(), OptionId = option.Id, Language = lang, Value = request.Value.Trim() });
        else
            translation.Value = request.Value.Trim();

        await RecordAsync("SpecificationOptionTranslated", specification, $"An option of \"{specification.Name}\" translated into {lang}",
            before, new { Value = request.Value.Trim() }, cancellationToken);
        await _specifications.SaveChangesAsync(cancellationToken);
        return Read(specification);
    }

    public async Task Handle(DeleteSpecificationOptionCommand request, CancellationToken cancellationToken)
    {
        var specification = await FindAsync(request.CategoryId, request.SpecificationId, cancellationToken);
        var option = FindOption(specification, request.OptionId);
        var used = await _specifications.ProductsUsingOptionAsync(option.Id, cancellationToken);
        if (used > 0)
            throw new ConflictException($"{used} product(s) are \"{option.Value}\". Change them first.");

        _specifications.RemoveOption(option);
        await RecordAsync("SpecificationOptionDeleted", specification, $"Option \"{option.Value}\" removed from \"{specification.Name}\"",
            new { option.Code, option.Value }, null, cancellationToken);
        await _specifications.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<SpecificationResponse>> Handle(GetCategorySpecificationsQuery request, CancellationToken cancellationToken)
    {
        if (await _categories.GetByIdAsync(request.CategoryId, cancellationToken) is null)
            throw new NotFoundException($"Category with ID '{request.CategoryId}' was not found.");

        var applicable = await Applicable.ToCategoryAsync(request.CategoryId, _categories, _specifications, cancellationToken);
        return applicable.Select(Read).ToList();
    }
}
