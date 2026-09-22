using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Catalog.Application.Products.Images.GetProductImage;
using Ecommerce.Catalog.Application.Products.Images.RemoveProductImage;
using Ecommerce.Catalog.Application.Products.Images.UploadProductImage;
using FluentValidation;
using FluentValidation.Results;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

public class ProductsController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetProductsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetProductByIdQuery(id));
        if (result == null)
        {
            return NotFound(new { message = "Product not found." });
        }
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Give a product its image, or replace it (specs/019). One multipart part named <c>file</c>;
    /// JPEG, PNG or WebP by content, at most 2 MB.
    /// </summary>
    /// <remarks>
    /// The size limit here refuses an enormous body before it is buffered - as a 400 "Request body too
    /// large", because MVC reports a failed form read as model state; the validator refuses anything
    /// over exactly 2 MB with a message about the image. Both are 400.
    /// </remarks>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/image")]
    [RequestSizeLimit(ProductImageKey.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProductImageKey.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> PutImage(Guid id, IFormFile? file)
    {
        if (file is null)
        {
            throw new ValidationException([new ValidationFailure("File", "Send the image as a multipart part named 'file'.")]);
        }

        await using var content = file.OpenReadStream();
        return Ok(await Mediator.Send(new UploadProductImageCommand(id, content, file.Length)));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        await Mediator.Send(new RemoveProductImageCommand(id));
        return NoContent();
    }

    /// <summary>
    /// A product's image, for anyone. Cacheable for good only when <c>v</c> names the current version -
    /// the address changes with the image, so that is safe (specs/019 D6).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImage(Guid id, [FromQuery] string? v)
    {
        var image = await Mediator.Send(new GetProductImageQuery(id));
        if (image is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = v == image.Version ? "public, max-age=31536000, immutable" : "no-cache";
        // The type was decided from the bytes; the browser must not second-guess it.
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Content, image.ContentType);
    }
}
