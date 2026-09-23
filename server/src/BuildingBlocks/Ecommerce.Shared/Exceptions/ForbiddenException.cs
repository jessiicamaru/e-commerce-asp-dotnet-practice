namespace Ecommerce.Shared.Exceptions;

/// <summary>
/// 403 with a sentence the caller may read (specs/043): the caller is known and the answer is no - a
/// locked account after the right password, a moderator reaching past what moderators may do.
/// </summary>
/// <remarks>
/// Not for "this is not yours": that stays a 404, because a 403 confirms the thing exists (specs/027).
/// </remarks>
public class ForbiddenException(string message) : Exception(message);
