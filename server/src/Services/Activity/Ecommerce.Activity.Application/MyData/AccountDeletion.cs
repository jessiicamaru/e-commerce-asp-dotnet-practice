using MediatR;

namespace Ecommerce.Activity.Application.MyData;

public interface IAccountErasure
{
    /// <summary>Deletes the person's notices; keeps the audit log without their email - as an actor or in a summary - and without the snapshots of their own details (specs/112). One transaction; idempotent.</summary>
    Task EraseAsync(Guid personId, string email, CancellationToken cancellationToken = default);
}

/// <summary>An account was deleted (<c>AccountDeleted</c>): take the person out of Activity's rows.</summary>
public record EraseAccountCommand(Guid UserId, string Email) : IRequest;

public class EraseAccountCommandHandler(IAccountErasure erasure) : IRequestHandler<EraseAccountCommand>
{
    private readonly IAccountErasure _erasure = erasure;

    public Task Handle(EraseAccountCommand request, CancellationToken cancellationToken) =>
        _erasure.EraseAsync(request.UserId, request.Email, cancellationToken);
}
