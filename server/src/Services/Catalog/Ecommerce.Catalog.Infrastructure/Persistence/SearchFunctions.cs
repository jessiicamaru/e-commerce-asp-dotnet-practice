namespace Ecommerce.Catalog.Infrastructure.Persistence;

/// <summary>
/// The catalogue search's SQL (specs/074, #113).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Unaccent"/> maps to <c>f_unaccent(text)</c>, created by the <c>AddSearchIndexes</c> migration: an
/// IMMUTABLE wrapper around <c>unaccent</c> with its dictionary named. <c>unaccent()</c> itself is only STABLE,
/// so PostgreSQL will not build an index on an expression using it, and every search was a sequential scan of
/// products and translations. The GIN trigram indexes are over exactly <c>f_unaccent(lower("Name"))</c>, so the
/// query must say exactly that for the planner to use them.
/// </para>
/// <para>
/// Only callable inside a query: EF translates the call to SQL and never runs this body.
/// </para>
/// </remarks>
public static class SearchFunctions
{
    public static string Unaccent(string input) =>
        throw new InvalidOperationException("f_unaccent runs in PostgreSQL, inside a query.");

    /// <summary>
    /// The LIKE escape character, named in the query. ⚠️ Npgsql writes <c>ESCAPE ''</c> - no escape at all - when a
    /// <c>EF.Functions.Like</c> names none, so without this the pattern's backslashes were literal and "50%" found
    /// nothing (the test that caught it: <c>A_term_with_percent_or_underscore_is_matched_literally</c>).
    /// </summary>
    public const string Escape = "\\";

    /// <summary>
    /// A term as a LIKE pattern that matches it anywhere, literally: its own <c>%</c>, <c>_</c> and <c>\</c> are
    /// escaped with <see cref="Escape"/> - so "50%" finds "50%", not "50" and anything.
    /// </summary>
    public static string ContainsPattern(string term) =>
        "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
