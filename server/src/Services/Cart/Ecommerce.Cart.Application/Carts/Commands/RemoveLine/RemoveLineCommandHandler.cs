using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using MediatR;

namespace Ecommerce.Cart.Application.Carts.Commands.RemoveLine;

/// <remarks>Removing something that is not there is not an error - it is already the outcome asked for.</remarks>
public class RemoveLineCommandHandler(
    ICartRepository carts,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<RemoveLineCommand>
{
    private readonly ICartRepository _carts = carts;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(RemoveLineCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var cart = await _carts.GetForUpdateAsync(userId, ct);
            var line = cart?.Lines.FirstOrDefault(l => l.ProductId == request.ProductId);

            if (line is null)
            {
                return;
            }

            _carts.RemoveLine(line);
            cart!.UpdatedAt = DateTime.UtcNow;
            await _carts.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
