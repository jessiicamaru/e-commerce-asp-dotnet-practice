using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Application.Addresses.Commands.SetDefaultAddress;

public class SetDefaultAddressCommandHandler(
    IAddressRepository addresses,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditTrail audit) : IRequestHandler<SetDefaultAddressCommand>
{
    private readonly IAddressRepository _addresses = addresses;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;

    public async Task Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // The lock is what makes two concurrent "set default" calls both succeed: without it they
            // race into the partial unique index and one caller gets a 500.
            await _addresses.LockOwnerAsync(userId, ct);
            var all = await _addresses.ListAsync(userId, ct);

            var target = all.FirstOrDefault(a => a.Id == request.Id)
                ?? throw new NotFoundException("Address not found.");

            if (target.IsDefault)
            {
                return;
            }

            var now = DateTime.UtcNow;

            // Demote first and save, then promote: never two defaults, even for one statement.
            foreach (var current in all.Where(a => a.IsDefault))
            {
                current.IsDefault = false;
                current.UpdatedAt = now;
            }

            await _addresses.SaveChangesAsync(ct);

            target.IsDefault = true;
            target.UpdatedAt = now;
            // "Changed", never the address itself - like every address entry (specs/041, #128).
            await _audit.RecordAsync(AuditCategory.User, "DefaultAddressChanged", "Address", target.Id.ToString(),
                "Chose another default delivery address", cancellationToken: ct);
            await _addresses.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
