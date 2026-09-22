using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.SetQuantity;

public class SetQuantityCommandHandler(
    ICartRepository carts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<SetQuantityCommand>
{
    private readonly ICartRepository _carts = carts;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(SetQuantityCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var cart = await _carts.GetForUpdateAsync(userId, ct);
            var line = cart?.Lines.FirstOrDefault(l => l.SellableId == request.ProductId);

            if (line is null)
            {
                // Setting something to zero that is already absent has already happened.
                if (request.Quantity == 0)
                {
                    return;
                }

                throw new NotFoundException($"Product {request.ProductId} is not in the cart.");
            }

            if (request.Quantity == 0)
            {
                // A line at zero is a line that does not exist. The database refuses a zero, too.
                _carts.RemoveLine(line);
            }
            else
            {
                line.Quantity = request.Quantity;
            }

            cart!.UpdatedAt = DateTime.UtcNow;
            await _carts.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
