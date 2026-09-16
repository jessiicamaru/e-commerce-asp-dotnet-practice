using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.ExpireStock;

/// <summary>Sweeps reservations past their holding period. Returns how many were reclaimed.</summary>
public record ExpireStockCommand(int BatchSize = 100) : IRequest<int>;
