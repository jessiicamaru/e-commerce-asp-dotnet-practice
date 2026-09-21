namespace Ecommerce.Application.Addresses.Common;

/// <summary>The parts of an address a customer supplies. Shared by the save and update commands.</summary>
public interface IAddressFields
{
    string RecipientName { get; }
    string Line1 { get; }
    string? Line2 { get; }
    string City { get; }
    string? Region { get; }
    string PostalCode { get; }
    string Country { get; }
    string? Phone { get; }
}
