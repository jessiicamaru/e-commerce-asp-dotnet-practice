using MediatR;

namespace Ecommerce.Catalog.Application.MyData;

public interface IAccountErasure
{
    /// <summary>
    /// Takes the person out of Catalog (specs/112 data-model): reviews and questions stay without their name; saved
    /// products, review rights and reports they filed go; a seller's shop closes. One transaction; idempotent.
    /// </summary>
    Task EraseAsync(Guid personId, DateTime deletedAt, CancellationToken cancellationToken = default);
}

/// <summary>An account was deleted (<c>AccountDeleted</c>): take the person out of Catalog's rows.</summary>
public record EraseAccountCommand(Guid UserId, DateTime DeletedAt) : IRequest;

public class EraseAccountCommandHandler(IAccountErasure erasure) : IRequestHandler<EraseAccountCommand>
{
    private readonly IAccountErasure _erasure = erasure;

    public Task Handle(EraseAccountCommand request, CancellationToken cancellationToken) =>
        _erasure.EraseAsync(request.UserId, request.DeletedAt, cancellationToken);
}
