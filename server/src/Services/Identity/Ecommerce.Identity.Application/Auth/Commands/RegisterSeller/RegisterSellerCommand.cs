using System.Text;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Common.Constants;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit;
using MediatR;

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
    string ShopName
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
    }
}

public class RegisterSellerCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IPublishEndpoint publishEndpoint) : IRequestHandler<RegisterSellerCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<AuthResponse> Handle(RegisterSellerCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.GetByEmailAsync(request.Email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        // Both roles. A seller who cannot buy is a strange kind of account, and every customer-only
        // endpoint - the cart, an order, an address - would otherwise refuse them.
        var sellerRole = await _roleRepository.GetByNameAsync(RoleNames.Seller, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The '{RoleNames.Seller}' role is missing. The database has not been seeded.");

        var customerRole = await _roleRepository.GetByNameAsync(RoleNames.Customer, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The '{RoleNames.Customer}' role is missing. The database has not been seeded.");

        var user = new User
        {
            Email = request.Email.Trim(),   // stored as typed; compared through EmailKey (#49)
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Roles = { sellerRole, customerRole },
        };

        await _userRepository.AddAsync(user, cancellationToken);

        var shopName = request.ShopName.Trim();

        await _userRepository.AddSellerProfileAsync(new SellerProfile
        {
            UserId = user.Id,
            ShopName = shopName,
        }, cancellationToken);

        // Staged, then published, then saved - one transaction holding the account, the shop and the
        // announcement (constitution III). Publishing after the save would allow a registered seller
        // that Catalog is never told about, whose products would then read as the shop's own forever.
        await _publishEndpoint.Publish(
            new SellerRegisteredEvent(user.Id, shopName, DateTime.UtcNow), cancellationToken);

        await _userRepository.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var refreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay),
        });

        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            accessToken,
            refreshTokenString
        );
    }
}
