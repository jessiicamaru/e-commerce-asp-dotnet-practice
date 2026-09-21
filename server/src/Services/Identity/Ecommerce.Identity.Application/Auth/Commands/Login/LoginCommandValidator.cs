using FluentValidation;

namespace Ecommerce.Application.Auth.Commands.Login;

/// <summary>
/// Both fields present, so an empty body is a 400 rather than a database lookup (issue #43).
/// </summary>
/// <remarks>
/// Deliberately nothing more. A length or format rule here would tell a caller something about which
/// passwords are possible, and an account created before a rule changed must still be able to sign in.
/// A wrong password stays the single 401 from #28.
/// </remarks>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}
