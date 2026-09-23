using Ecommerce.Activity.Application.Audit;

namespace Ecommerce.Activity.Tests;

/// <summary>The field-level diff (specs/041 research D5) - pure, so every shape gets a case.</summary>
public class AuditDiffTests
{
    [Fact]
    public void An_update_lists_only_the_fields_that_changed()
    {
        var changes = AuditDiff.Compute(
            """{"name":"Canon R50","price":18500000,"active":true}""",
            """{"name":"Canon EOS R50","price":18500000,"active":false}""");

        Assert.Equal(["active", "name"], changes.Select(c => c.Path));
        var name = changes.Single(c => c.Path == "name");
        Assert.Equal("Canon R50", name.Before!.Value.GetString());
        Assert.Equal("Canon EOS R50", name.After!.Value.GetString());
    }

    [Fact]
    public void Nested_objects_and_arrays_are_named_by_their_path()
    {
        var changes = AuditDiff.Compute(
            """{"prices":{"VND":100,"USD":4},"options":[{"value":"Black"},{"value":"Body"}]}""",
            """{"prices":{"VND":120,"USD":4},"options":[{"value":"Silver"},{"value":"Body"}]}""");

        Assert.Equal(["options[0].value", "prices.VND"], changes.Select(c => c.Path));
    }

    [Fact]
    public void A_creation_lists_every_field_as_added_and_a_deletion_as_removed()
    {
        var created = AuditDiff.Compute(null, """{"a":1,"b":"x"}""");
        var deleted = AuditDiff.Compute("""{"a":1}""", null);

        Assert.All(created, c => Assert.Null(c.Before));
        Assert.Equal(["a", "b"], created.Select(c => c.Path));
        Assert.Null(Assert.Single(deleted).After);
    }

    [Fact]
    public void A_field_that_appears_or_disappears_is_a_change()
    {
        var changes = AuditDiff.Compute("""{"a":1,"gone":2}""", """{"a":1,"new":3}""");

        Assert.Equal(["gone", "new"], changes.Select(c => c.Path));
        Assert.Null(changes[0].After);
        Assert.Null(changes[1].Before);
    }

    [Fact]
    public void Nothing_changed_is_no_change()
    {
        Assert.Empty(AuditDiff.Compute("""{"a":[1,2],"b":{"c":null}}""", """{"a":[1,2],"b":{"c":null}}"""));
        Assert.Empty(AuditDiff.Compute(null, null));
    }

    [Fact]
    public void A_value_changing_type_is_a_change()
    {
        Assert.Single(AuditDiff.Compute("""{"a":1}""", """{"a":"1"}"""));
    }

    [Fact]
    public void A_huge_diff_is_cut_short()
    {
        var after = "{" + string.Join(",", Enumerable.Range(0, 500).Select(i => $"\"f{i}\":{i}")) + "}";

        Assert.Equal(AuditDiff.MaxChanges, AuditDiff.Compute(null, after).Count);
    }
}
