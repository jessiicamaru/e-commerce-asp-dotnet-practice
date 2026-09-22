using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Contracts.Catalog;
using Ecommerce.Shared.Exceptions;
using MassTransit;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Commands.CreateProduct;

public class CreateProductCommandHandler(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IPublishEndpoint publishEndpoint
) : IRequestHandler<CreateProductCommand, ProductResponse>
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<ProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var existingCategory = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);

        if (existingCategory == null)
        {
            throw new NotFoundException($"Category with ID '{request.CategoryId}' was not found.");
        }

        var existingProduct = await _productRepository.GetBySkuAsync(request.Sku, cancellationToken);

        if (existingProduct != null)
        {
            throw new ConflictException($"Product with SKU '{request.Sku}' already exists.");
        }

        if (await _productRepository.VariantSkuExistsAsync(request.Sku, cancellationToken))
        {
            throw new ConflictException($"A variant with SKU '{request.Sku}' already exists.");
        }

        var product = new Product
        {
            // Set here, not left to EF: the first variant reuses it (specs/020 research D2), and a
            // value the database generates is not known until SaveChanges. ADR-001 asks for v7 anyway.
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Sku = request.Sku,
            CategoryId = request.CategoryId
        };

        // Every product is sold in at least one shape, so creating one creates its first variant -
        // the thing that actually carries the price and the sku (specs/020). Options are optional: a
        // product sold one way has nothing to choose between.
        var options = (request.Options ?? [])
            .Select(option => new VariantOption
            {
                Id = Guid.CreateVersion7(),
                Name = option.Name.Trim(),
                Value = option.Value.Trim(),
            })
            .ToList();

        var variant = new ProductVariant
        {
            // The product's own id, like the migration's backfill (specs/020 research D2). It keeps
            // "one shape" products simple everywhere, and the stock row ProductCreatedEvent causes
            // Inventory to create is then keyed by the thing that is actually bought.
            Id = product.Id,
            Sku = request.Sku,
            Price = request.Price,
            Options = options,
            OptionSummary = ProductVariant.Summarise(options),
        };

        foreach (var option in options)
        {
            option.VariantId = variant.Id;
        }

        product.Variants.Add(variant);

        await _productRepository.AddAsync(product, cancellationToken);

        // Publish Domain Event via MassTransit Outbox (staged in DbContext ChangeTracker)
        await _publishEndpoint.Publish(new ProductCreatedEvent(
            product.Id,
            product.Name,
            product.Price,
            product.Sku,
            product.CategoryId,
            DateTime.UtcNow
        ), cancellationToken);

        // Save BOTH Product entity and OutboxMessage in 1 single atomic DB transaction
        await _productRepository.SaveChangesAsync(cancellationToken);

        return ProductResponse.WithVariants(product);
    }
}
