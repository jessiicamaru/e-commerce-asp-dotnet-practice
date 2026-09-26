using System.Text.Json;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Entities;
using MediatR;

namespace Ecommerce.Application.Email;

/// <summary>Keeps an email request until it is sent (specs/060) - once, however often it is delivered.</summary>
public record QueueEmailCommand(EmailRequested Request) : IRequest<bool>;

public class QueueEmailCommandHandler(IOutgoingEmailRepository emails, IUserRepository users) : IRequestHandler<QueueEmailCommand, bool>
{
    private readonly IOutgoingEmailRepository _emails = emails;
    private readonly IUserRepository _users = users;

    public async Task<bool> Handle(QueueEmailCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        var now = DateTime.UtcNow;

        // No language asked for (EmailTemplate.ReadersLanguage): the sender - Catalog telling a saver, say - cannot
        // know it, and Identity can: the one the person last used the shop in (specs/083), else the default.
        var language = string.IsNullOrWhiteSpace(r.Language)
            ? (await _users.GetByIdAsync(r.RecipientId, cancellationToken))?.Language
            : r.Language;

        return await _emails.QueueAsync(new OutgoingEmail
        {
            Id = r.EmailId,
            RecipientId = r.RecipientId,
            Template = r.Template,
            DataJson = JsonSerializer.Serialize(r.Data ?? []),
            Language = string.IsNullOrWhiteSpace(language) ? EmailTemplates.DefaultLanguage : language.ToLowerInvariant(),
            Status = OutgoingEmailStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        }, cancellationToken);
    }
}
