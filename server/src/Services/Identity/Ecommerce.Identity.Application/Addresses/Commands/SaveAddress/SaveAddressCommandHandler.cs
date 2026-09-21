using Ecommerce.Application.Addresses.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Application.Addresses.Commands.SaveAddress;

public class SaveAddressCommandHandler(
    IAddressRepository addresses,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRequestHandler<SaveAddressCommand, AddressResponse>
{
    private readonly IAddressRepository _addresses = addresses;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<AddressResponse> Handle(SaveAddressCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        DeliveryAddress? saved = null;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Under the owner's lock, so two saves cannot both see 19 and both add a 20th, and two
            // first-ever saves cannot both decide they are the default.
            await _addresses.LockOwnerAsync(userId, ct);
            var existing = await _addresses.ListAsync(userId, ct);

            if (existing.Count >= AddressFieldsValidator<SaveAddressCommand>.MaxAddressesPerCustomer)
            {
                throw new ConflictException(
                    $"An address book holds at most {AddressFieldsValidator<SaveAddressCommand>.MaxAddressesPerCustomer} addresses.");
            }

            var now = DateTime.UtcNow;
            saved = new DeliveryAddress
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                RecipientName = request.RecipientName.Trim(),
                Line1 = request.Line1.Trim(),
                Line2 = string.IsNullOrWhiteSpace(request.Line2) ? null : request.Line2.Trim(),
                City = request.City.Trim(),
                Region = string.IsNullOrWhiteSpace(request.Region) ? null : request.Region.Trim(),
                PostalCode = request.PostalCode.Trim(),
                Country = request.Country.Trim().ToUpperInvariant(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                // The first address is the default without being asked (FR-002).
                IsDefault = existing.Count == 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            _addresses.Add(saved);
            await _addresses.SaveChangesAsync(ct);
        }, cancellationToken);

        return AddressResponse.From(saved!);
    }
}
