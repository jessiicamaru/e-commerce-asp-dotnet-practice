using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Addresses.Common;

public record AddressResponse(
    Guid Id,
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string Country,
    string? Phone,
    bool IsDefault)
{
    public static AddressResponse From(DeliveryAddress a) => new(
        a.Id, a.RecipientName, a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.Country, a.Phone, a.IsDefault);
}
