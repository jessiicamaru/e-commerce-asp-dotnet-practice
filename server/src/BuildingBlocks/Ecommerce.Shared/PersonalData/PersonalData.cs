namespace Ecommerce.Shared.PersonalData;

/// <summary>
/// A table that holds something about the person but is not handed out, and why - said in the export rather than
/// silently left out (#217, specs/111 research D3).
/// </summary>
public record WithheldTable(string Table, string Reason);

/// <summary>
/// What one service holds about the signed-in person (#217, specs/111): its rows, grouped into sections, and the
/// tables it withholds with the reason. Every section is a list, even of one row, so each reads the same way.
/// </summary>
public record MyDataResponse(
    string Service,
    DateTime ExportedAt,
    IReadOnlyDictionary<string, IReadOnlyList<object>> Sections,
    IReadOnlyList<WithheldTable> Withheld);

/// <summary>
/// Where a service keeps personal data (#217, specs/111 research D2): every table of its model is exported (into a named
/// section), withheld with a reason the person reads, or not personal. A test holds the declaration to the model, so a
/// new table cannot be forgotten - the export then says everything, and deleting an account (specs/112) finds everything.
/// </summary>
public class PersonalDataInventory
{
    /// <summary>
    /// The transactional outbox's bookkeeping (MassTransit), in every service that publishes: delivery state, emptied as
    /// messages are delivered. Not personal, and declared once here rather than in every inventory.
    /// </summary>
    public static readonly IReadOnlyCollection<string> OutboxTables = ["InboxState", "OutboxMessage", "OutboxState"];

    public required string Service { get; init; }

    /// <summary>Table → the export's section it is read into. Several tables may feed one section.</summary>
    public required IReadOnlyDictionary<string, string> Exported { get; init; }

    public IReadOnlyList<WithheldTable> Withheld { get; init; } = [];

    public IReadOnlyCollection<string> NotPersonal { get; init; } = [];

    /// <summary>
    /// The sections a deletion keeps (specs/112), each with why the shop keeps it - the books, or other people's use -
    /// always without the person's name, email, phone or address. Every other section is erased.
    /// </summary>
    public IReadOnlyDictionary<string, string> Kept { get; init; } = new Dictionary<string, string>();

    /// <summary>The sections a deletion empties: every exported section not <see cref="Kept"/>.</summary>
    public IReadOnlyCollection<string> Erased => Sections.Except(Kept.Keys).ToList();

    /// <summary>The sections an export of this service must have.</summary>
    public IReadOnlyCollection<string> Sections => Exported.Values.Distinct().Order().ToList();

    /// <summary>
    /// What is wrong with this declaration against the tables the service's model maps: a table declared nowhere, a
    /// table declared twice, or a declared table the model no longer has. Empty when it is right.
    /// </summary>
    public IReadOnlyList<string> Problems(IEnumerable<string> modelTables)
    {
        var tables = modelTables.Where(t => !string.IsNullOrEmpty(t)).Distinct().ToHashSet();
        var declared = Exported.Keys.Concat(Withheld.Select(w => w.Table)).Concat(NotPersonal).ToList();

        var problems = new List<string>();
        problems.AddRange(tables.Except(declared).Except(OutboxTables).Order()
            .Select(t => $"{Service}: table '{t}' is not declared - exported, withheld with a reason, or not personal"));
        problems.AddRange(declared.GroupBy(t => t).Where(g => g.Count() > 1)
            .Select(g => $"{Service}: table '{g.Key}' is declared more than once"));
        problems.AddRange(declared.Distinct().Except(tables).Order()
            .Select(t => $"{Service}: table '{t}' is declared but the model has no such table"));
        problems.AddRange(Withheld.Where(w => string.IsNullOrWhiteSpace(w.Reason))
            .Select(w => $"{Service}: withheld table '{w.Table}' gives the person no reason"));
        problems.AddRange(Kept.Keys.Except(Sections).Order()
            .Select(s => $"{Service}: kept section '{s}' is not a section of the export"));
        problems.AddRange(Kept.Where(k => string.IsNullOrWhiteSpace(k.Value))
            .Select(k => $"{Service}: kept section '{k.Key}' gives no reason for keeping it"));
        return problems;
    }

    /// <summary>An export of these sections, in this inventory's words.</summary>
    public MyDataResponse Answer(IReadOnlyDictionary<string, IReadOnlyList<object>> sections, DateTime now) =>
        new(Service, now, sections, Withheld);
}
