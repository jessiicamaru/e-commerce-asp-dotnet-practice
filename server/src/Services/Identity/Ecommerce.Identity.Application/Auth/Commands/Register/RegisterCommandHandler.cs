using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Common.Constants;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using MediatR;

namespace Ecommerce.Application.Auth.Commands.Register;

public class RegisterCommandHandler(
IUserRepository userRepository,
IRoleRepository roleRepository,
IPasswordHasher passwordHasher,
IJwtTokenGenerator jwtTokenGenerator
    ) : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (existingUser != null)
        {
            throw new Exception("Email is already registered");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // Self-registration always yields a plain shopper. Admin is granted out of band:
        // by the startup data initializer, or later by an existing administrator.
        var customerRole = await _roleRepository.GetByNameAsync(RoleNames.Customer, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The '{RoleNames.Customer}' role is missing. The database has not been seeded.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Roles = { customerRole }
        };

        await _userRepository.AddAsync(user, cancellationToken);

        await _userRepository.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);

        var refreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay)
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