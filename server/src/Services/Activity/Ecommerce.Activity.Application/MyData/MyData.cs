using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.PersonalData;
using MediatR;

namespace Ecommerce.Activity.Application.MyData;

/// <summary>What Activity holds about the signed-in person (#217, specs/111) - from the token.</summary>
public record GetMyDataQuery : IRequest<MyDataResponse>;

public interface IPersonalDataReader
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class ActivityPersonalData
{
    /// <summary>
    /// Every table of Activity's model, declared (specs/111 research D2). ⚠️ A new table goes here or <c>MyDataTests</c>
    /// fails; deleting an account (specs/112) reads the same list.
    /// </summary>
    public static readonly PersonalDataInventory Inventory = new()
    {
        Service = "activity",
        Exported = new Dictionary<string, string> { ["notifications"] = "notifications", ["audit_entries"] = "activity" },
        NotPersonal = ["notification_wording_versions"],
    };
}

public class GetMyDataQueryHandler(IPersonalDataReader reader, ICurrentUser currentUser) : IRequestHandler<GetMyDataQuery, MyDataResponse>
{
    private readonly IPersonalDataReader _reader = reader;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<MyDataResponse> Handle(GetMyDataQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        return ActivityPersonalData.Inventory.Answer(await _reader.ReadAsync(me, cancellationToken), DateTime.UtcNow);
    }
}
