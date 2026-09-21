namespace Ecommerce.Order.Application.Common.Interfaces;

/// <summary>
/// What is in the calling customer's cart, asked of the Cart service at checkout.
/// </summary>
/// <remarks>
/// <para>
/// <b>No user id parameter, deliberately.</b> The implementation forwards the customer's own bearer
/// token, and Cart identifies them from it through ICurrentUser. A GetCart(userId) would let anything
/// that can reach Cart read anybody's cart by naming them - the UserId-in-the-body defect again, one
/// hop further in. Constitution IV, preserved across a service boundary.
/// </para>
/// <para>
/// No transport here; the gRPC client is in Infrastructure.
/// </para>
/// </remarks>
public interface ICartReader
{
    /// <exception cref="Shared.Exceptions.DependencyUnavailableException">Cart could not be asked.</exception>
    Task<IReadOnlyList<CartItem>> GetMyCartAsync(CancellationToken cancellationToken = default);
}

public record CartItem(Guid ProductId, int Quantity);
