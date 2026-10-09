using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Application.Products.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Queries.GetProducts;

public record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    string? SearchTerm = null,
    string? SortBy = null,
    /// <summary>One shop's products (#197, specs/099) - on the shelf only, like the rest of the listing.</summary>
    Guid? SellerId = null,
    /// <summary>The "from" price is at least this, in the request's currency (#216, specs/109).</summary>
    decimal? MinPrice = null,
    /// <summary>The "from" price is at most this, in the request's currency.</summary>
    decimal? MaxPrice = null,
    /// <summary>Only what can be bought now - the availability read model.</summary>
    bool InStock = false,
    // specs/161: only products with a variant reduced in the currency asked for.
    bool OnSale = false,
    /// <summary>Products holding every one of these specification options (#366, specs/159) - Brand: Apple.</summary>
    List<Guid>? OptionIds = null
) : IRequest<PaginatedList<ProductResponse>>;

public class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice is not null);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0).When(x => x.MaxPrice is not null);
        // A reversed range is a mistake better said than answered with an empty page.
        RuleFor(x => x.MinPrice).LessThanOrEqualTo(x => x.MaxPrice!.Value)
            .When(x => x.MinPrice is not null && x.MaxPrice is not null)
            .WithMessage("The minimum price is above the maximum.");
    }
}
