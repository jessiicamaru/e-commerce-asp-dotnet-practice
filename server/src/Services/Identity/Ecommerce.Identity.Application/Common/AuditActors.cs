using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Application.Common;

/// <summary>
/// A user as the actor of an audit entry (specs/041) - for the moments nobody is signed in yet: signing in,
/// registering.
/// </summary>
public static class AuditActors
{
    private static readonly string[] Ranked = ["Admin", "Moderator", "Seller", "Customer"];

    public static AuditActor Of(User user) =>
        new(user.Id, user.Email, Ranked.FirstOrDefault(r => user.Roles.Any(role => role.Name == r)));
}
