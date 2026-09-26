using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Shared.Authentication;

/// <summary>
/// Every controller action says who may call it (#183, specs/089): <c>[Authorize]</c> or <c>[AllowAnonymous]</c>,
/// on the action or on its controller. Each service's tests assert <see cref="Undeclared"/> is empty.
/// </summary>
/// <remarks>
/// The fallback policy in <see cref="DependencyInjection.AddJwtAuthentication"/> already refuses an action that
/// says nothing, so this is not what keeps it private. It keeps the decision in the source, where a reviewer
/// reads it: before, <c>AuthController</c>'s sign-in was public only because nothing said otherwise, and the next
/// action added there would have been public for the same reason - "the mistake looks completely ordinary in
/// review" (constitution, Principle IV).
/// </remarks>
public static class EndpointAccess
{
    /// <summary>The actions in <paramref name="assembly"/> that neither require nor waive authorization, as <c>Controller.Action</c>.</summary>
    public static IReadOnlyList<string> Undeclared(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !m.IsDefined(typeof(NonActionAttribute), inherit: true))
                .Where(action => !Declares(action) && !Declares(controller))
                .Select(action => $"{controller.Name}.{action.Name}"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static bool Declares(MemberInfo member) =>
        member.GetCustomAttributes(inherit: true).Any(a => a is IAuthorizeData or IAllowAnonymous);
}
