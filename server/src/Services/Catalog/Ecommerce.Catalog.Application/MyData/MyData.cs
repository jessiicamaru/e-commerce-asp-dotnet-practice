using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.PersonalData;
using MediatR;

namespace Ecommerce.Catalog.Application.MyData;

/// <summary>What Catalog holds about the signed-in person (#217, specs/111) - from the token.</summary>
public record GetMyDataQuery : IRequest<MyDataResponse>;

public interface IPersonalDataReader
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class CatalogPersonalData
{
    /// <summary>
    /// Every table of Catalog's model, declared (specs/111 research D2). ⚠️ A new table goes here or <c>MyDataTests</c>
    /// fails; deleting an account (specs/112) reads the same list.
    /// </summary>
    public static readonly PersonalDataInventory Inventory = new()
    {
        Kept = new Dictionary<string, string>
        {
            ["reviews"] = "Other shoppers rely on them; they stay without your name.",
            ["questions"] = "A seller's answer hangs on each; they stay without your name.",
            ["products"] = "A seller's listings stay off the shelf, for the orders that name them.",
            ["shop"] = "A closed shop stays, for the orders that name it.",
        },
        Service = "catalog",
        Exported = new Dictionary<string, string>
        {
            ["product_reviews"] = "reviews",
            ["product_questions"] = "questions",
            ["saved_products"] = "savedProducts",
            ["content_reports"] = "reports",
            ["review_eligibility"] = "reviewEligibility",
            ["sellers"] = "shop",
            ["products"] = "products",
        },
        Withheld =
        [
            new("product_viewers", "A hash of a session or browser id that marks a product view as counted once; it cannot be traced back to you."),
        ],
        NotPersonal =
        [
            "categories", "category_translations", "product_translations", "product_variants", "variant_options",
            "variant_option_translations", "variant_prices", "product_views",
        ],
    };
}

public class GetMyDataQueryHandler(IPersonalDataReader reader, ICurrentUser currentUser) : IRequestHandler<GetMyDataQuery, MyDataResponse>
{
    private readonly IPersonalDataReader _reader = reader;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<MyDataResponse> Handle(GetMyDataQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        return CatalogPersonalData.Inventory.Answer(await _reader.ReadAsync(me, cancellationToken), DateTime.UtcNow);
    }
}
