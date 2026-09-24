namespace Ecommerce.Shared.Exceptions;

/// <summary>
/// 403 with a sentence the caller may read (specs/043): the caller is known and the answer is no - a
/// locked account after the right password, a moderator reaching past what moderators may do.
/// </summary>
/// <remarks>
/// Not for "this is not yours": that stays a 404, because a 403 confirms the thing exists (specs/027).
/// <para>
/// <paramref name="facts"/> travel beside the sentence as ProblemDetails extensions (specs/049), so a
/// client can word the refusal in its reader's language - e.g. <c>code</c>, <c>until</c>, <c>reason</c> on
/// a sign-in refusal. They are shown in every environment, like the message, so only put in them what the
/// message may already say.
/// </para>
/// </remarks>
public class ForbiddenException(string message, IReadOnlyDictionary<string, object?>? facts = null) : Exception(message)
{
    public IReadOnlyDictionary<string, object?> Facts { get; } = facts ?? new Dictionary<string, object?>();
}
