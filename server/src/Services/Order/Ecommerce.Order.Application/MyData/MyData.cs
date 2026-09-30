using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.PersonalData;
using MediatR;

namespace Ecommerce.Order.Application.MyData;

/// <summary>What Order holds about the signed-in person (#217, specs/111) - from the token.</summary>
public record GetMyDataQuery : IRequest<MyDataResponse>;

public interface IPersonalDataReader
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class OrderPersonalData
{
    /// <summary>
    /// Every table of Order's model, declared (specs/111 research D2). ⚠️ A new table goes here or <c>MyDataTests</c>
    /// fails; deleting an account (specs/112) reads the same list.
    /// </summary>
    public static readonly PersonalDataInventory Inventory = new()
    {
        Kept = new Dictionary<string, string>
        {
            ["orders"] = "The shop's books: kept for accounting, without your name, email, phone or address.",
            ["returns"] = "The shop's books: kept for accounting, without your name, email, phone or address.",
            ["voucherUses"] = "The shop's books: kept for accounting, without your name, email, phone or address.",
            ["voucherUseCounts"] = "The shop's books: kept for accounting, without your name, email, phone or address.",
            ["vouchers"] = "The shop's books: kept for accounting, without your name, email, phone or address.",
            ["payouts"] = "The shop's books: kept for accounting, without your name, email, phone or address.",
        },
        Service = "order",
        Exported = new Dictionary<string, string>
        {
            ["orders"] = "orders",
            ["order_items"] = "orders",
            ["order_shipments"] = "orders",
            ["parcel_returns"] = "returns",
            ["voucher_redemptions"] = "voucherUses",
            ["voucher_customer_uses"] = "voucherUseCounts",
            ["vouchers"] = "vouchers",
            ["payouts"] = "payouts",
        },
        NotPersonal =
        [
            "voucher_conditions", "voucher_targets", "voucher_amounts", "delivery_options", "delivery_option_prices", "carriers",
        ],
    };
}

public class GetMyDataQueryHandler(IPersonalDataReader reader, ICurrentUser currentUser) : IRequestHandler<GetMyDataQuery, MyDataResponse>
{
    private readonly IPersonalDataReader _reader = reader;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<MyDataResponse> Handle(GetMyDataQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        return OrderPersonalData.Inventory.Answer(await _reader.ReadAsync(me, cancellationToken), DateTime.UtcNow);
    }
}
