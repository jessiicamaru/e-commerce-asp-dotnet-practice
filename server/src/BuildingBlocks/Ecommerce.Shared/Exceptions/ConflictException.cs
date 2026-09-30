namespace Ecommerce.Shared.Exceptions;

/// <summary>409: the request is understood and allowed, but the state it meets says no.</summary>
/// <remarks>
/// <paramref name="facts"/> travel as ProblemDetails extensions, as a <see cref="ForbiddenException"/>'s do (specs/049) -
/// since specs/112 for a deletion refused with <c>code</c> and <c>reasons</c>, which the storefront words itself.
/// </remarks>
public class ConflictException(string message, IReadOnlyDictionary<string, object?>? facts = null) : Exception(message)
{
    public IReadOnlyDictionary<string, object?> Facts { get; } = facts ?? new Dictionary<string, object?>();
}
