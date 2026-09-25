namespace Ecommerce.Inventory.Domain.Entities;

/// <summary>
/// A returned parcel whose units this service put back on the shelf (specs/066) - one row per return, claimed
/// first, so a redelivered message finds it and puts nothing back twice.
/// </summary>
public class ReturnedParcel
{
    public Guid ReturnId { get; set; }

    public Guid OrderId { get; set; }

    public DateTime RestockedAt { get; set; }
}
