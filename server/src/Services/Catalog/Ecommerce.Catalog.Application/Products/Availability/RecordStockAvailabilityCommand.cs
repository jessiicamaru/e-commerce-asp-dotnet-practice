using MediatR;

namespace Ecommerce.Catalog.Application.Products.Availability;

/// <summary>
/// Records what Inventory said about a product's buyability.
/// </summary>
/// <param name="ObservedAt">
/// Inventory's observation time, carried on the announcement. <b>Not</b> the time this arrived — an
/// announcement can overtake an older one in flight, and the older one must lose.
/// </param>
/// <remarks>
/// Returns whether this call was the one that recorded the answer. <c>false</c> is ordinary: a
/// duplicate, an overtaken observation, or a product this catalogue does not hold.
/// </remarks>
/// <param name="VariantId">
/// The sellable unit this is about (specs/020). Stock is counted per variant, so a black body can be
/// in stock while a silver kit is not. An announcement from an older Inventory carries no variant, and
/// the consumer passes the product id, which for every product that existed before variants IS its
/// only variant's id (research D2).
/// </param>
public record RecordStockAvailabilityCommand(
    Guid ProductId,
    bool IsAvailable,
    DateTime ObservedAt,
    Guid VariantId
) : IRequest<bool>;
