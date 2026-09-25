using Ecommerce.Application.Common;
using System.Text;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Auth.Commands.EmailConfirmation;
using Ecommerce.Application.Common.Constants;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Application.Auth.Commands.RegisterSeller;

/// <summary>
/// Registers somebody who sells, with the name their shop trades under (specs/027).
/// </summary>
/// <remarks>
/// <b>A separate endpoint rather than a flag on <c>register</c>.</b> A boolean in a request body that
/// changes what an account <i>is</i> has exactly the shape of the two defects this project has already
/// fixed - <c>UserId</c> in a body (specs/009) and a price in a body (issue #18). Two endpoints cannot
/// be confused by accident; one endpoint with a privilege switch in its payload can.
/// </remarks>
public record RegisterSellerCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string ShopName,
    string? Description = null,
    string? Phone = null,
    string Language = ""   // the confirmation email's language; the controller reads Accept-Language (specs/063)
) : IRequest<AuthResponse>;

public class RegisterSellerCommandValidator : AbstractValidator<RegisterSellerCommand>
{
    public RegisterSellerCommandValidator()
    {
        // The same rules as an ordinary registration - stated by reusing them rather than by
        // copying them, so the two cannot drift into accepting different passwords.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email must be at most 255 characters.")
            .EmailAddress().WithMessage("Email is not a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(RegisterCommandValidator.MinimumPasswordLength)
            .WithMessage($"Password must be at least {RegisterCommandValidator.MinimumPasswordLength} characters.")
            .Must(p => p is null || Encoding.UTF8.GetByteCount(p) <= RegisterCommandValidator.MaximumPasswordBytes)
            .WithMessage($"Password must be at most {RegisterCommandValidator.MaximumPasswordBytes} bytes.");

        RuleFor(x => x.FirstName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must be at most 100 characters.");

        RuleFor(x => x.LastName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must be at most 100 characters.");

        // What a shopper will see under every product this person lists.
        RuleFor(x => x.ShopName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Shop name is required.")
            .MaximumLength(100).WithMessage("Shop name must be at most 100 characters.");

        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Phone).MaximumLength(20);
    }
}

/// <summary>
/// Opens an account and applies to sell, in one step (specs/044). The account is a customer's; the shop
/// waits for a moderator or an administrator - until specs/044 it opened here, at once, for anybody.
/// </summary>
public class RegisterSellerCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IShopApplicationRepository applications,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IAuditTrail audit,
    EmailConfirmations confirmations) : IRequestHandler<RegisterSellerCommand, AuthResponse>
{
    private readonly EmailConfirmations _confirmations = confirmations;
    private readonly IAuditTrail _audit = audit;

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IShopApplicationRepository _applications = applications;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<AuthResponse> Handle(RegisterSellerCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.GetByEmailAsync(request.Email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var customerRole = await _roleRepository.GetByNameAsync(RoleNames.Customer, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The '{RoleNames.Customer}' role is missing. The database has not been seeded.");

        // Customer only. Seller comes with the approval - a shop that could list products before anybody
        // looked at it is the thing specs/044 exists to stop.
        var user = new User
        {
            Email = request.Email.Trim(),   // stored as typed; compared through EmailKey (#49)
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Roles = { customerRole },
        };

        await _userRepository.AddAsync(user, cancellationToken);

        var application = new ShopApplication
        {
            UserId = user.Id,
            ShopName = request.ShopName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
        };
        await _applications.AddAsync(application, cancellationToken);

        await _audit.RecordAsync(
            AuditCategory.User, "ShopApplied", "ShopApplication", application.Id.ToString(),
            $"{user.Email} registered and applied to open \"{application.ShopName}\"",
            after: new { user.Email, user.FirstName, user.LastName, application.ShopName, Roles = user.Roles.Select(r => r.Name) },
            actor: AuditActors.Of(user), cancellationToken: cancellationToken);

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var refreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay),
        });

        // The link to confirm the address (specs/063): the shop is approved only once it is used.
        await _confirmations.StageAsync(user, request.Language, DateTime.UtcNow, cancellationToken);

        // One save: the account, the application, the audit entry, the link and the session commit together.
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            accessToken,
            refreshTokenString,
            user.Roles.Select(role => role.Name).ToList(),
            user.EmailConfirmed
        );
    }
}
