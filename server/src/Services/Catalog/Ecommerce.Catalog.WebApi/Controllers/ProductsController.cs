using Ecommerce.Catalog.Application.Products.Commands.CreateProduct;
using Ecommerce.Catalog.Application.Products.Commands.DeleteProduct;
using Ecommerce.Catalog.Application.Products.Prices;
using Ecommerce.Catalog.Application.Products.Variants.AddProductVariant;
using Ecommerce.Catalog.Application.Products.Variants.UpdateProductVariant;
using Ecommerce.Catalog.Application.Products.Images;
using Ecommerce.Catalog.Application.Products.Images.GetProductImage;
using Ecommerce.Catalog.Application.Products.Images.RemoveProductImage;
using Ecommerce.Catalog.Application.Products.Images.UploadProductImage;
using Ecommerce.Catalog.Application.Products.Translations;
using FluentValidation;
using FluentValidation.Results;
using Ecommerce.Catalog.Application.Products.Queries.GetMyProducts;
using Ecommerce.Catalog.Application.Products.Queries.GetProductById;
using Ecommerce.Catalog.Application.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.WebApi.Controllers;

public class ProductsController : ApiControllerBase
{
    // Every write below is Seller-or-Admin. The attribute decides who may TRY; SellerOwnership in the
    // handler decides whose product they may try it on, and an attribute cannot do the second because
    // it runs before any row has been read (specs/027).
    //
    // ⚠️ Leaving these as Admin-only made the ownership checks UNREACHABLE for sellers: a seller was
    // refused at the door, so the code deciding whether the listing was hers never ran. Every unit
    // test still passed, because they send commands straight to the handlers. The running stack is
    // what showed it - a seller got 403 on her own product.
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

    /// <summary>The caller's own listings, and only theirs (specs/027). Takes no seller id.</summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] GetMyProductsQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>
    /// Lists a product. <b>A seller's product is theirs; an administrator's belongs to the shop
    /// itself</b> (specs/027) - who it belongs to comes from the token, never from the body.
    /// </summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Another shape of the product: a kit, a colour, a size (specs/020). The variant carries the sku
    /// and the price, and it is what a customer actually buys.
    /// </summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPost("{id:guid}/variants")]
    public async Task<IActionResult> AddVariant(Guid id, [FromBody] VariantRequest request)
    {
        var variant = await Mediator.Send(new AddProductVariantCommand(
            id, request.Sku, request.Price, request.Options ?? []));

        return CreatedAtAction(nameof(GetById), new { id }, variant);
    }

    /// <summary>
    /// Re-prices a variant or takes it off sale. The sku and the options never change: an order froze
    /// them, and it has to keep describing what was bought.
    /// </summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPut("{id:guid}/variants/{variantId:guid}")]
    public async Task<IActionResult> UpdateVariant(Guid id, Guid variantId, [FromBody] UpdateVariantRequest request)
    {
        return Ok(await Mediator.Send(new UpdateProductVariantCommand(id, variantId, request.Price, request.IsActive)));
    }

    /// <summary>
    /// This product's name and description in one language (specs/021). An upsert: writing it twice
    /// leaves the second text, not a conflict.
    /// </summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPut("{id:guid}/translations/{language}")]
    public async Task<IActionResult> SetTranslation(Guid id, string language, [FromBody] TranslationRequest request)
    {
        return Ok(await Mediator.Send(new SetProductTranslationCommand(id, language, request.Name, request.Description)));
    }

    /// <summary>Takes a language away; the product falls back to its default text.</summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpDelete("{id:guid}/translations/{language}")]
    public async Task<IActionResult> RemoveTranslation(Guid id, string language)
    {
        await Mediator.Send(new RemoveProductTranslationCommand(id, language));
        return NoContent();
    }

    /// <summary>
    /// One option in one language - <c>Kit: Body only</c> → <c>Bộ: Chỉ thân máy</c>. Option values are
    /// read by customers as much as names are.
    /// </summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPut("{id:guid}/options/{optionId:guid}/translations/{language}")]
    public async Task<IActionResult> SetOptionTranslation(
        Guid id, Guid optionId, string language, [FromBody] OptionTranslationRequest request)
    {
        await Mediator.Send(new SetOptionTranslationCommand(id, optionId, language, request.Name, request.Value));
        return NoContent();
    }

    /// <summary>
    /// What this variant costs in one currency (specs/022). An upsert, like a translation.
    /// </summary>
    /// <remarks>
    /// The amount is stored exactly as given and <b>nothing converts it</b>. Setting the shop's
    /// default currency writes the variant's own price, which is where that one number lives.
    /// </remarks>
    [Authorize(Roles = "Seller,Admin")]
    [HttpPut("{id:guid}/variants/{variantId:guid}/prices/{currency}")]
    public async Task<IActionResult> SetVariantPrice(
        Guid id, Guid variantId, string currency, [FromBody] VariantPriceRequest request)
    {
        return Ok(await Mediator.Send(new SetVariantPriceCommand(id, variantId, currency, request.Amount)));
    }

    /// <summary>
    /// Stops selling this variant in this currency. It is then reported with no price rather than
    /// with a converted one. Refused for the default currency, which has no row to remove.
    /// </summary>
    [Authorize(Roles = "Seller,Admin")]
    [HttpDelete("{id:guid}/variants/{variantId:guid}/prices/{currency}")]
    public async Task<IActionResult> RemoveVariantPrice(Guid id, Guid variantId, string currency)
    {
        await Mediator.Send(new RemoveVariantPriceCommand(id, variantId, currency));
        return NoContent();
    }

    /// <summary>
    /// Removes a product and every shape of it from the catalogue, for good (specs/024).
    /// </summary>
    /// <remarks>
    /// <b>Not the way to stop selling something</b> - that is deactivation, which leaves the row
    /// where a cart and a report can still find it. This is for rows that should never have existed.
    /// Orders are unaffected: each one froze what it bought, which is what freezing is for.
    /// </remarks>
    [Authorize(Roles = "Seller,Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteProductCommand(id));
        return NoContent();
    }

    public record TranslationRequest(string Name, string? Description);

    public record OptionTranslationRequest(string Name, string Value);

    public record VariantPriceRequest(decimal Amount);

    public record VariantRequest(string Sku, decimal Price, List<VariantOptionInput>? Options);

    public record UpdateVariantRequest(decimal Price, bool IsActive = true);

    /// <summary>
    /// Give a product its image, or replace it (specs/019). One multipart part named <c>file</c>;
    /// JPEG, PNG or WebP by content, at most 2 MB.
    /// </summary>
    /// <remarks>
    /// The size limit here refuses an enormous body before it is buffered - as a 400 "Request body too
    /// large", because MVC reports a failed form read as model state; the validator refuses anything
    /// over exactly 2 MB with a message about the image. Both are 400.
    /// </remarks>
    [Authorize(Roles = "Seller,Admin")]
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

    [Authorize(Roles = "Seller,Admin")]
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
