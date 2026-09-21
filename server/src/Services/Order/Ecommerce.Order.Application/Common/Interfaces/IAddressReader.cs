namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>
/// One of the calling customer's delivery addresses, asked of Identity at checkout.
/// </summary>
/// <remarks>
/// <b>No user id parameter, deliberately</b> - the same reasoning as <see cref="ICartReader"/>. The
/// implementation forwards the customer's own token, and Identity decides whose addresses they are.
/// </remarks>
public interface IAddressReader
{
    /// <param name="addressId">An address of the caller's; <c>null</c> for their default.</param>
    /// <returns>
    /// The address, or <c>null</c> when it does not exist, is not the caller's, or - for the default -
    /// the caller has none. Identity does not say which, on purpose.
    /// </returns>
    /// <exception cref="Shared.Exceptions.DependencyUnavailableException">Identity could not be asked.</exception>
    Task<AddressCopy?> GetMyAddressAsync(Guid? addressId, CancellationToken cancellationToken = default);
}

public record AddressCopy(
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string Country,
    string? Phone);
