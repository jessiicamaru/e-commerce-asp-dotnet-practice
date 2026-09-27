namespace Ecommerce.Domain.Entities;

/// <summary>
/// Where a seller's payouts go (#213, specs/106). One per seller. The account number is a secret the shop needs to pay
/// them: it is read in full only by an administrator making a transfer, and shown to everybody else - the seller
/// included - as its last four characters (research D4).
/// </summary>
public class SellerPayoutAccount
{
    /// <summary>The seller's user id.</summary>
    public Guid SellerId { get; set; }

    public string BankName { get; set; } = string.Empty;

    public string AccountHolder { get; set; } = string.Empty;

    /// <summary>Letters and digits only, spaces removed - 6 to 34 characters (34 is the longest IBAN).</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>When it was last set - what the administrator's "changed recently" warning reads.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The number as anybody but an administrator paying sees it.</summary>
    public string Masked => Mask(AccountNumber);

    public static string Mask(string number) => "•••• " + (number.Length <= 4 ? number : number[^4..]);

    public string Last4 => AccountNumber.Length <= 4 ? AccountNumber : AccountNumber[^4..];
}
