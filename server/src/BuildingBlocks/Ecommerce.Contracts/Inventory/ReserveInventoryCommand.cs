using Ecommerce.Contracts.Order;

namespace Ecommerce.Contracts.Inventory;

public record ReserveInventoryCommand(
    Guid OrderId,
    List<OrderItemDto> Items
);

public record InventoryReservedEvent(
    Guid OrderId,
    DateTime ReservedAt
);

public record InventoryReservationFailedEvent(
    Guid OrderId,
    string Reason
);

public record ReleaseInventoryCommand(
    Guid OrderId,
    string Reason
);
