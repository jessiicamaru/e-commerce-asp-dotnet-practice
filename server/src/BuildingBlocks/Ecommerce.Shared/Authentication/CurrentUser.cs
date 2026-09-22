using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ecommerce.Shared.Authentication;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    // The token carries the user id in "sub". Claim names survive verbatim because
    // JwtBearer is configured with MapInboundClaims = false.
    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : null;

    public string? Email => Principal?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    // Not Principal.IsInRole: that reads ClaimTypes.Role, and the signing side writes the SHORT
    // name "role" with MapInboundClaims = false and RoleClaimType = "role" (see AddJwtAuthentication).
    // Asking for the claim by the name that is actually in the token is the reliable way.
    public bool IsInRole(string role) =>
        Principal?.FindAll("role").Any(claim => claim.Value == role) ?? false;
}
