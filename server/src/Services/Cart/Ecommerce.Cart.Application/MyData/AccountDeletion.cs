using MediatR;

namespace Ecommerce.Cart.Application.MyData;

public interface IAccountErasure
{
    /// <summary>Deletes the person's cart (its lines go with it) and their checkout outcomes (specs/112). One transaction; idempotent.</summary>
    Task EraseAsync(Guid personId, string email, CancellationToken cancellationToken = default);
}

/// <summary>An account was deleted (<c>AccountDeleted</c>): take the person out of Cart's rows.</summary>
public record EraseAccountCommand(Guid UserId, string Email) : IRequest;

public class EraseAccountCommandHandler(IAccountErasure erasure) : IRequestHandler<EraseAccountCommand>
{
    private readonly IAccountErasure _erasure = erasure;

    public Task Handle(EraseAccountCommand request, CancellationToken cancellationToken) =>
        _erasure.EraseAsync(request.UserId, request.Email, cancellationToken);
}
