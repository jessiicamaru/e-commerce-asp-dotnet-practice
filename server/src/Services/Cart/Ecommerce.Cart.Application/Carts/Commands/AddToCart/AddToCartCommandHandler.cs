using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Cart.Domain.Entities;
using Ecommerce.Shared.Authentication;
using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.AddToCart;

/// <remarks>
/// Does not ask Catalog whether the product exists. A write to a cart should work when Catalog is
/// down, and an unknown product is not lost - it is shown as no longer available when the cart is
/// read, and checkout refuses it. Validating here would make adding to a cart depend on another
/// service for no gain the read does not already provide.
/// </remarks>
public class AddToCartCommandHandler(
    ICartRepository carts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<AddToCartCommand>
{
    private readonly ICartRepository _carts = carts;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var cart = await _carts.GetOrCreateForUpdateAsync(userId, ct);
            var now = DateTime.UtcNow;

            // Lines are matched by the SELLABLE unit: two shapes of one product are two lines.
            var sellableId = request.VariantId ?? request.ProductId;
            var line = cart.Lines.FirstOrDefault(l => l.SellableId == sellableId);

            if (line is null)
            {
                cart.Lines.Add(new CartLine
                {
                    Id = Guid.CreateVersion7(),
                    CartId = cart.Id,
                    ProductId = request.ProductId,
                    VariantId = request.VariantId,
                    Quantity = request.Quantity,
                    AddedAt = now
                });
            }
            else
            {
                // Adding again raises the quantity - one line per variant, never two.
                line.Quantity += request.Quantity;
            }

            cart.UpdatedAt = now;
            await _carts.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
