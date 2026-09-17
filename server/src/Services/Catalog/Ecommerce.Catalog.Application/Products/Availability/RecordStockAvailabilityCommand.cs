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
public record RecordStockAvailabilityCommand(
    Guid ProductId,
    bool IsAvailable,
    DateTime ObservedAt
) : IRequest<bool>;
