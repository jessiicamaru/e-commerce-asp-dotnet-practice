using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);

    string GenerateRefreshToken();
}