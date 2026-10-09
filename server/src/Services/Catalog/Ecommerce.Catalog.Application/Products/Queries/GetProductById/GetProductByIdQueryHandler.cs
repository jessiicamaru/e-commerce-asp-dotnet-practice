using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Queries.GetProductById;

public class GetProductByIdQueryHandler(
    IProductRepository productRepository,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization,
    IRequestCurrency currency,
    IOptions<CurrencyOptions> money,
    ISellerRepository sellers,
    Ecommerce.Shared.Authentication.ICurrentUser currentUser,
    ICategoryRepository categories,
    ISpecificationRepository specifications)
    : IRequestHandler<GetProductByIdQuery, ProductResponse?>
{
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<ProductResponse?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        // Not on the shelf (specs/045): its seller and staff see it; to anybody else it does not exist,
        // the same 404 as a product that never did.
        if (product == null || !ProductReview.MaySee(product, currentUser))
        {
            return null;
        }

        var shopNames = product.SellerId is null
            ? []
            : await sellers.GetNamesAsync([product.SellerId.Value], cancellationToken);

        // Its specifications (specs/159): those of its category and its department, with this product's values.
        var applicable = await Specifications.Applicable.ToCategoryAsync(product.CategoryId, categories, specifications, cancellationToken);
        var table = Specifications.Applicable.Table(
            applicable, await specifications.ValuesOfAsync(product.Id, cancellationToken),
            language.Current, localization.Value.DefaultLanguage);

        return ProductResponse.WithVariants(
            product,
            language.Current,
            localization.Value.DefaultLanguage,
            currency.Current.Code,
            money.Value.DefaultCurrency,
            product.SellerId is not null && shopNames.TryGetValue(product.SellerId.Value, out var shop)
                ? shop
                : null) with
            {
                Specifications = table,
                // The gallery after the cover (specs/160). Only a reader allowed to see the product gets here, so the
                // addresses' keys (specs/081) go only where the product's own does.
                Photos = Images.ProductGallery.Respond(await _productRepository.GetPhotosAsync(product.Id, cancellationToken)),
            };
    }
}
