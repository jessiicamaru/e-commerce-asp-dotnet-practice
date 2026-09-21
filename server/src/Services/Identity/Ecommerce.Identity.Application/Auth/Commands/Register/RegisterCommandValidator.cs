using System.Text;
using FluentValidation;

namespace Ecommerce.Application.Auth.Commands.Register;

/// <summary>
/// What a new account must look like (issue #43). Before this, any body created an account - an email
/// of <c>not-an-email</c>, a password of <c>1</c> and empty names all returned 200 and a token.
/// </summary>
/// <remarks>
/// <para>
/// <b>Length, not composition.</b> NIST SP 800-63B recommends a minimum length and advises against
/// rules like "one symbol, one digit", which push people towards predictable passwords. The minimum is
/// the 8 the admin bootstrap already enforces. A check against breached passwords would be the next
/// step; it needs a list or a service, and is not built.
/// </para>
/// <para>
/// <b>At most 72 bytes, because BCrypt reads no further.</b> Everything past byte 72 is silently
/// ignored, so two long passwords sharing their first 72 bytes would both open the account. Refusing
/// the longer one is honest; accepting it would promise a strength it does not have.
/// </para>
/// Name and email limits match the <c>users</c> columns, so a long value is a 400 rather than a
/// database error.
/// </remarks>
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordBytes = 72;

    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email must be at most 255 characters.")
            .EmailAddress().WithMessage("Email is not a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinimumPasswordLength)
            .WithMessage($"Password must be at least {MinimumPasswordLength} characters.")
            .Must(p => p is null || Encoding.UTF8.GetByteCount(p) <= MaximumPasswordBytes)
            .WithMessage($"Password must be at most {MaximumPasswordBytes} bytes (about {MaximumPasswordBytes} letters, fewer with accents or emoji).");

        RuleFor(x => x.FirstName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must be at most 100 characters.");

        RuleFor(x => x.LastName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must be at most 100 characters.");
    }
}
