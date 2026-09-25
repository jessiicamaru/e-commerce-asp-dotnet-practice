using System.Text.Json;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Entities;
using MediatR;

namespace Ecommerce.Application.Email;

/// <summary>Keeps an email request until it is sent (specs/060) - once, however often it is delivered.</summary>
public record QueueEmailCommand(EmailRequested Request) : IRequest<bool>;

public class QueueEmailCommandHandler(IOutgoingEmailRepository emails) : IRequestHandler<QueueEmailCommand, bool>
{
    private readonly IOutgoingEmailRepository _emails = emails;

    public Task<bool> Handle(QueueEmailCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var now = DateTime.UtcNow;

        return _emails.QueueAsync(new OutgoingEmail
        {
            Id = r.EmailId,
            RecipientId = r.RecipientId,
            Template = r.Template,
            DataJson = JsonSerializer.Serialize(r.Data ?? []),
            Language = string.IsNullOrWhiteSpace(r.Language) ? EmailTemplates.DefaultLanguage : r.Language.ToLowerInvariant(),
            Status = OutgoingEmailStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        }, cancellationToken);
    }
}
