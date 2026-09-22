using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <summary>
    /// Rewrites every variant's stored <c>OptionSummary</c> with its options in a fixed order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing ordered the option rows, so the summary was built in whatever order the database
    /// yielded them: two shapes of one camera listed theirs as <c>Colour: … · Kit: …</c> and
    /// <c>Kit: … · Colour: …</c>. <c>ProductVariant.Summarise</c> now orders by name, but that only
    /// fixes summaries written from now on.
    /// </para>
    /// <para>
    /// <b>Without this, the same variant reads two different ways.</b> A request that names a
    /// language rebuilds the summary from the option rows, ordered; a request that names none - and
    /// the gRPC call that FREEZES the summary onto an order line - uses the stored string, unordered.
    /// That is exactly the drift <c>Summarise</c> exists to prevent, in a new place.
    /// </para>
    /// <para>
    /// Data only: no column changes, so an earlier image reads these rows exactly as it read the old
    /// ones. Orders keep the text they froze, because it is theirs.
    /// </para>
    /// </remarks>
    public partial class NormaliseOptionSummaryOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE product_variants v
                SET "OptionSummary" = COALESCE((
                    SELECT string_agg(o."Name" || ': ' || o."Value", ' · ' ORDER BY lower(o."Name"))
                    FROM variant_options o
                    WHERE o."VariantId" = v."Id"
                ), '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty. The previous order was whatever the database happened to return,
            // which is not a state anything can be restored to - and an earlier image reads these
            // summaries perfectly well, because only their word order changed.
        }
    }
}
