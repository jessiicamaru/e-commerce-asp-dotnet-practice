using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.EmptyCart;

public class EmptyCartCommandHandler(
    ICartRepository carts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<EmptyCartCommand>
{
    private readonly ICartRepository _carts = carts;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(EmptyCartCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var cart = await _carts.GetForUpdateAsync(userId, ct);

            if (cart is null || cart.Lines.Count == 0)
            {
                return;
            }

            foreach (var line in cart.Lines.ToList())
            {
                _carts.RemoveLine(line);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _carts.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
