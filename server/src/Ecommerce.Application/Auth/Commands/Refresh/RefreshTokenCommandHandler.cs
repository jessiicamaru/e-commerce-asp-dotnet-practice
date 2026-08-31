using MediatR;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Domain.Entities;
using Ecommerce.Application.Common.Constants;

namespace Ecommerce.Application.Auth.Commands.Refresh;

public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenGenerator jwtTokenGenerator
) : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUserRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (user == null)
        {
            throw new Exception("Invalid session.");
        }

        var activeToken = user.RefreshTokens.FirstOrDefault(t => t.Token == request.RefreshToken);
        if (activeToken == null || activeToken.IsExpired || activeToken.RevokedAt != null)
        {
            throw new Exception("Session expired or invalid.");
        }

        user.RefreshTokens.Remove(activeToken);

        var newAccessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var newRefreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = newRefreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay)
        });

        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            newAccessToken,
            newRefreshTokenString
        );
    }
}
