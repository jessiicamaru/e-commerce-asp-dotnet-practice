using System.Text.RegularExpressions;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;

namespace Ecommerce.Application.Sellers;

/// <summary>A seller's payout account as they, and anybody but an administrator paying, see it (specs/106).</summary>
public record PayoutAccountResponse(string BankName, string AccountHolder, string AccountNumberMasked, DateTime UpdatedAt)
{
    public static PayoutAccountResponse From(SellerPayoutAccount a) => new(a.BankName, a.AccountHolder, a.Masked, a.UpdatedAt);
}

/// <summary>In full - what an administrator transfers to. The one place the whole number leaves Identity (research D4).</summary>
public record PayoutAccountFullResponse(Guid SellerId, string BankName, string AccountHolder, string AccountNumber, DateTime UpdatedAt);

/// <summary>The caller's own payout account. There is no id to pass.</summary>
public record GetMyPayoutAccountQuery : IRequest<PayoutAccountResponse>;

/// <summary>Sets the caller's payout account (#213, specs/106).</summary>
public record SetMyPayoutAccountCommand(string BankName, string AccountHolder, string AccountNumber) : IRequest<PayoutAccountResponse>;

/// <summary>Administrators: these sellers' accounts in full, to pay them. A seller with none is absent.</summary>
public record GetPayoutAccountsQuery(IReadOnlyList<Guid> SellerIds) : IRequest<List<PayoutAccountFullResponse>>;

/// <summary>Order, recording a payout: the account as it may be frozen - never the full number (research D2).</summary>
public record GetPayoutAccountForPayoutQuery(Guid SellerId) : IRequest<PayoutAccountForPayout?>;

public record PayoutAccountForPayout(string BankName, string AccountHolder, string Last4, DateTime UpdatedAt);

public static class PayoutAccountRules
{
    /// <summary>What is stored: letters and digits, spaces and dashes a person typed removed.</summary>
    public static string Normalise(string? number) => Regex.Replace(number ?? string.Empty, @"[\s-]", string.Empty).ToUpperInvariant();
}

public class SetMyPayoutAccountCommandValidator : AbstractValidator<SetMyPayoutAccountCommand>
{
    public SetMyPayoutAccountCommandValidator()
    {
        RuleFor(x => x.BankName).Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("The bank is required.").MaximumLength(100);
        RuleFor(x => x.AccountHolder).Must(h => !string.IsNullOrWhiteSpace(h)).WithMessage("The account holder is required.").MaximumLength(100);
        RuleFor(x => x.AccountNumber)
            .Must(n => Regex.IsMatch(PayoutAccountRules.Normalise(n), "^[A-Z0-9]{6,34}$"))
            .WithMessage("The account number must be 6 to 34 letters or digits.");
    }
}

public class GetPayoutAccountsQueryValidator : AbstractValidator<GetPayoutAccountsQuery>
{
    public GetPayoutAccountsQueryValidator() => RuleFor(x => x.SellerIds).Must(ids => ids.Count <= 100).WithMessage("At most 100 sellers at once.");
}

public class PayoutAccountHandlers(IUserRepository users, ICurrentUser currentUser, IAuditTrail audit, IEmailSender email) :
    IRequestHandler<GetMyPayoutAccountQuery, PayoutAccountResponse>,
    IRequestHandler<SetMyPayoutAccountCommand, PayoutAccountResponse>,
    IRequestHandler<GetPayoutAccountsQuery, List<PayoutAccountFullResponse>>,
    IRequestHandler<GetPayoutAccountForPayoutQuery, PayoutAccountForPayout?>
{
    private readonly IUserRepository _users = users;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly IEmailSender _email = email;

    public async Task<PayoutAccountResponse> Handle(GetMyPayoutAccountQuery request, CancellationToken cancellationToken)
    {
        var seller = await SellerAsync(cancellationToken);
        var account = await _users.GetPayoutAccountAsync(seller, cancellationToken)
            ?? throw new NotFoundException("No payout account yet.");
        return PayoutAccountResponse.From(account);
    }

    public async Task<PayoutAccountResponse> Handle(SetMyPayoutAccountCommand request, CancellationToken cancellationToken)
    {
        var seller = await SellerAsync(cancellationToken);
        var account = await _users.GetPayoutAccountAsync(seller, cancellationToken);
        var before = account is null ? null : new { account.BankName, account.AccountHolder, Account = account.Masked };

        if (account is null)
        {
            account = new SellerPayoutAccount { SellerId = seller };
            await _users.AddPayoutAccountAsync(account, cancellationToken);
        }

        account.BankName = request.BankName.Trim();
        account.AccountHolder = request.AccountHolder.Trim();
        account.AccountNumber = PayoutAccountRules.Normalise(request.AccountNumber);
        account.UpdatedAt = DateTime.UtcNow;

        // ⚠️ Masked by hand: the audit redacts by property NAME, and "Account" is no secret word to it (research D4).
        await _audit.RecordAsync(AuditCategory.User, "PayoutAccountSet", "Seller", seller.ToString(),
            "Payout account set", before, new { account.BankName, account.AccountHolder, Account = account.Masked },
            cancellationToken: cancellationToken);
        // Always, and to the address on the account: a changed payout account is how payout fraud starts, and a stolen
        // session cannot stop the real owner hearing of it (research D3).
        await _email.SendAsync(seller, EmailTemplate.PayoutAccountChanged,
            new Dictionary<string, string> { ["bank"] = account.BankName, ["last4"] = account.Last4 },
            EmailTemplate.ReadersLanguage, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return PayoutAccountResponse.From(account);
    }

    public async Task<List<PayoutAccountFullResponse>> Handle(GetPayoutAccountsQuery request, CancellationToken cancellationToken) =>
        (await _users.GetPayoutAccountsAsync(request.SellerIds, cancellationToken))
            .Select(a => new PayoutAccountFullResponse(a.SellerId, a.BankName, a.AccountHolder, a.AccountNumber, a.UpdatedAt))
            .ToList();

    public async Task<PayoutAccountForPayout?> Handle(GetPayoutAccountForPayoutQuery request, CancellationToken cancellationToken) =>
        await _users.GetPayoutAccountAsync(request.SellerId, cancellationToken) is { } a
            ? new PayoutAccountForPayout(a.BankName, a.AccountHolder, a.Last4, a.UpdatedAt)
            : null;

    /// <summary>The caller, when they sell; anybody else has no payout account to have (as for a shop name, specs/027).</summary>
    private async Task<Guid> SellerAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        _ = await _users.GetSellerProfileAsync(userId, cancellationToken) ?? throw new NotFoundException("This account does not sell on the shop.");
        return userId;
    }
}
