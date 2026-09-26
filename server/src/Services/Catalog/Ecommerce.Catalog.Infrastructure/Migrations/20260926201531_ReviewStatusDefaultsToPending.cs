using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <summary>
    /// A product inserted without a review status waits for review (#184, specs/093).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The column's default was 'Approved' so that every product from before review existed stayed on sale
    /// (20260923210256_AddProductReview). That backfill is long done, and the default now matters only to an image
    /// from before specs/045 running after a rollback: it inserts a seller's new product without the column, and
    /// 'Approved' put it on sale unreviewed. 'Pending' puts it in the moderators' queue - and the shop's own product,
    /// which such an image inserts the same way, waits too: the safer of the two mistakes.
    /// </para>
    /// <para>
    /// ⚠️ SQL, not <c>HasDefaultValue</c> in the model, on purpose. The enum's CLR default is <c>Approved</c>, and EF
    /// leaves a property holding its CLR default out of an INSERT when the model declares a default - so every
    /// administrator's product would be filed as Pending. The model declares no default and EF always writes the
    /// column; this default is only for writers that do not know it. <c>ProductReviewTests</c> holds both halves.
    /// </para>
    /// <para>
    /// Only a default changes: no row is rewritten, and every image reads and writes the column as before.
    /// </para>
    /// </remarks>
    public partial class ReviewStatusDefaultsToPending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE products ALTER COLUMN "ReviewStatus" SET DEFAULT 'Pending';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE products ALTER COLUMN "ReviewStatus" SET DEFAULT 'Approved';""");
        }
    }
}
