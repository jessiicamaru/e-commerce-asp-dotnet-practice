using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.PersonalData;
using MediatR;

namespace Ecommerce.Payment.Application.MyData;

/// <summary>What Payment holds about the signed-in person (#217, specs/111) - from the token.</summary>
public record GetMyDataQuery : IRequest<MyDataResponse>;

public interface IPersonalDataReader
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class PaymentPersonalData
{
    /// <summary>
    /// Every table of Payment's model, declared (specs/111 research D2). ⚠️ A new table goes here or <c>MyDataTests</c>
    /// fails; deleting an account (specs/112) reads the same list.
    /// </summary>
    public static readonly PersonalDataInventory Inventory = new()
    {
        Kept = new Dictionary<string, string>
        {
            ["payments"] = "The shop's books: kept for accounting, without your name, email, phone or address.",
            ["refunds"] = "The shop's books: kept for accounting, without your name, email, phone or address.",
            ["paymentCheckouts"] = "The shop's books: each attempt to pay at a gateway, kept for accounting, without your name, email, phone or address.",
        },
        Service = "payment",
        Exported = new Dictionary<string, string>
        {
            ["payments"] = "payments",
            ["refunds"] = "refunds",
            ["payment_checkouts"] = "paymentCheckouts",
        },
    };
}

public class GetMyDataQueryHandler(IPersonalDataReader reader, ICurrentUser currentUser) : IRequestHandler<GetMyDataQuery, MyDataResponse>
{
    private readonly IPersonalDataReader _reader = reader;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<MyDataResponse> Handle(GetMyDataQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        return PaymentPersonalData.Inventory.Answer(await _reader.ReadAsync(me, cancellationToken), DateTime.UtcNow);
    }
}
