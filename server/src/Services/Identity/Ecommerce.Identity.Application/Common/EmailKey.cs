namespace Ecommerce.Application.Common;

/// <summary>
/// How two emails are compared: trimmed, then case-insensitively (issue #49).
/// </summary>
/// <remarks>
/// The email is still <b>stored as typed</b>. What changes is only whether two of them are the same
/// account. <c>A@x.com</c> and <c>a@x.com</c> used to be two accounts, each with its own password, and a
/// customer who capitalised their address at sign-in was told the password was wrong. The database
/// enforces the same rule with a unique index on <c>lower("Email")</c>. Lookups compare against
/// <c>lower(...)</c>, so they use that index.
/// </remarks>
public static class EmailKey
{
    public static string For(string email) => email.Trim().ToLowerInvariant();
}
