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
    ISellerRepository sellers)
    : IRequestHandler<GetProductByIdQuery, ProductResponse?>
{
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<ProductResponse?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return null;
        }

        var shopNames = product.SellerId is null
            ? []
            : await sellers.GetNamesAsync([product.SellerId.Value], cancellationToken);

        return ProductResponse.WithVariants(
            product,
            language.Current,
            localization.Value.DefaultLanguage,
            currency.Current.Code,
            money.Value.DefaultCurrency,
            product.SellerId is not null && shopNames.TryGetValue(product.SellerId.Value, out var shop)
                ? shop
                : null);
    }
}
