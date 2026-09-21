using System.Globalization;
using FluentValidation;

namespace Ecommerce.Application.Addresses.Common;

/// <summary>
/// Checks that an address is <b>well-formed</b> - and nothing more.
/// </summary>
/// <remarks>
/// A postcode of the right shape may not exist, and a parcel may not reach an address that passes every
/// rule here. Knowing that needs a postal database this project does not have, so the messages say
/// "not well-formed" and never "invalid address" or "undeliverable" (specs/011 FR-004, research D11).
/// Postcode shapes differ by country; the rule is deliberately permissive, because refusing a real
/// address is worse than accepting an odd one.
/// </remarks>
public abstract class AddressFieldsValidator<T> : AbstractValidator<T> where T : IAddressFields
{
    public const int MaxAddressesPerCustomer = 20;

    protected AddressFieldsValidator()
    {
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(100)
            .WithMessage("Recipient name is required and must be at most 100 characters.");
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(200)
            .WithMessage("Address line 1 is required and must be at most 200 characters.");
        RuleFor(x => x.Line2).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100)
            .WithMessage("City is required and must be at most 100 characters.");
        RuleFor(x => x.Region).MaximumLength(100);

        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .Matches(@"^[A-Za-z0-9][A-Za-z0-9 \-]{0,14}[A-Za-z0-9]$")
            .WithMessage("Postal code is not well-formed: 2 to 16 letters, digits, spaces or hyphens.");

        RuleFor(x => x.Country)
            .NotEmpty()
            .Must(BeARecognisedCountry)
            .WithMessage("Country is not well-formed: use a two-letter ISO 3166 code, such as VN or GB.");

        RuleFor(x => x.Phone)
            .MaximumLength(30)
            .Matches(@"^[0-9 +\-()]*$")
            .WithMessage("Phone is not well-formed: digits, spaces and + - ( ) only.");
    }

    private static bool BeARecognisedCountry(string? country)
    {
        if (country is null || country.Length != 2 || !country.All(char.IsAsciiLetter))
        {
            return false;
        }

        try
        {
            _ = new RegionInfo(country.ToUpperInvariant());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
