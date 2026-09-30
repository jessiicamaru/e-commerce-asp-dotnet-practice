using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.PersonalData;
using MediatR;

namespace Ecommerce.Application.MyData;

/// <summary>What Identity holds about the signed-in person (#217, specs/111) - from the token, never an id in the request.</summary>
public record GetMyDataQuery : IRequest<MyDataResponse>;

/// <summary>Reads the person's rows, section by section - the projections are what is handed out, and nothing else.</summary>
public interface IPersonalDataReader
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class IdentityPersonalData
{
    /// <summary>
    /// Every table of Identity's model, declared (specs/111 research D2). ⚠️ A new table goes here - exported, withheld with
    /// a reason, or not personal - or <c>MyDataTests</c> fails; deleting an account (specs/112) reads the same list.
    /// </summary>
    public static readonly PersonalDataInventory Inventory = new()
    {
        Kept = new Dictionary<string, string>
        {
            ["profile"] = "An empty row under your id, so the orders and payments other services keep for the books still point at something; your name, email, phone and password are gone from it.",
        },
        Service = "identity",
        Exported = new Dictionary<string, string>
        {
            ["users"] = "profile",
            ["user_roles"] = "profile",
            ["delivery_addresses"] = "addresses",
            ["seller_profiles"] = "shop",
            ["seller_payout_accounts"] = "payoutAccount",
            ["shop_applications"] = "shopApplications",
            ["outgoing_emails"] = "emails",
        },
        Withheld =
        [
            new("refresh_tokens", "The secrets of your signed-in sessions. Sign out to end a session."),
            new("password_reset_tokens", "Hashes of password-reset links; each works once, for 30 minutes."),
            new("email_confirmation_tokens", "Hashes of address-confirmation links; each works once."),
            new("two_factor_challenges", "Hashes of the five-minute step between your password and your code."),
            new("two_factor_recovery_codes", "Hashes of your recovery codes - you were shown the codes once."),
            new("sign_in_throttles", "A count of wrong passwords for your email, kept for minutes to slow guessing."),
        ],
        NotPersonal = ["roles", "email_template_versions"],
    };
}

public class GetMyDataQueryHandler(IPersonalDataReader reader, ICurrentUser currentUser) : IRequestHandler<GetMyDataQuery, MyDataResponse>
{
    private readonly IPersonalDataReader _reader = reader;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<MyDataResponse> Handle(GetMyDataQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        return IdentityPersonalData.Inventory.Answer(await _reader.ReadAsync(me, cancellationToken), DateTime.UtcNow);
    }
}
