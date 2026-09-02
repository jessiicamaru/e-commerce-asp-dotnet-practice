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

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            Sku = request.Sku,
            CategoryId = request.CategoryId
        };

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        // Publish Domain Event via MassTransit Outbox
        await _publishEndpoint.Publish(new ProductCreatedEvent(
            product.Id,
            product.Name,
            product.Price,
            product.Sku,
            product.CategoryId,
            DateTime.UtcNow
        ), cancellationToken);

        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.Sku,
            product.CategoryId,
            product.IsActive
        );
    }
}
