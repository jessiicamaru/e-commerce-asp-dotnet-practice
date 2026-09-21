using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Application.Addresses.Commands.DeleteAddress;

/// <remarks>
/// Deleting the default promotes the most recently created remaining address, so a customer who has
/// addresses always has exactly one default (FR-002).
/// </remarks>
public class DeleteAddressCommandHandler(
    IAddressRepository addresses,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<DeleteAddressCommand>
{
    private readonly IAddressRepository _addresses = addresses;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(DeleteAddressCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _addresses.LockOwnerAsync(userId, ct);
            var all = await _addresses.ListAsync(userId, ct);

            var target = all.FirstOrDefault(a => a.Id == request.Id)
                ?? throw new NotFoundException("Address not found.");

            _addresses.Remove(target);

            // Two saves in one transaction, on purpose: the default is removed BEFORE another is
            // promoted. In one save EF may order the UPDATE first, and the partial unique index would
            // see two defaults for a moment and refuse.
            await _addresses.SaveChangesAsync(ct);

            if (target.IsDefault)
            {
                var next = all.Where(a => a.Id != target.Id).MaxBy(a => a.CreatedAt);

                if (next is not null)
                {
                    next.IsDefault = true;
                    next.UpdatedAt = DateTime.UtcNow;
                    await _addresses.SaveChangesAsync(ct);
                }
            }
        }, cancellationToken);
    }
}
