namespace Ecommerce.Order.Domain.Entities;

/// <summary>
/// Where an order goes - a <b>copy</b> taken at checkout, not a reference to the address book.
/// </summary>
/// <remarks>
/// The customer's address book lives in Identity and can be edited or emptied at any time. An order is
/// a record of a transaction: editing an address next year must not move where last year's parcel went.
/// The same freezing rule feature 009 applied to prices (specs/011-order-shipping FR-008).
/// </remarks>
public class ShippingAddress
{
    public string RecipientName { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
