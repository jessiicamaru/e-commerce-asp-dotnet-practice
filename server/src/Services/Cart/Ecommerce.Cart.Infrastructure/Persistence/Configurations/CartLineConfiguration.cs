using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Cart.Infrastructure.Persistence.Configurations;

public class CartLineConfiguration : IEntityTypeConfiguration<Domain.Entities.CartLine>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.CartLine> builder)
    {
        builder.ToTable("cart_lines", t =>
            // A line at zero is a line that does not exist. The code removes it; the database makes
            // sure nothing else can leave one behind.
            t.HasCheckConstraint("CK_cart_lines_quantity_positive", "\"Quantity\" > 0"));

        builder.HasKey(l => l.Id);

        // The application generates this id (Guid.CreateVersion7, per ADR-001), so EF must be told it
        // is NEVER store-generated. Otherwise a Guid key defaults to ValueGeneratedOnAdd, and a new line
        // discovered through cart.Lines with its id already set is taken for an EXISTING row: EF issues
        // an UPDATE, it affects zero rows, and every add fails with DbUpdateConcurrencyException. All
        // eight Cart tests failed exactly that way on their first run.
        builder.Property(l => l.Id).ValueGeneratedNever();

        // One line per product per cart - adding again raises the quantity instead of duplicating.
        // One line per SELLABLE unit (specs/020): two shapes of one product are two lines, and adding
        // the same shape twice raises its quantity instead.
        //
        // NULLS NOT DISTINCT because VariantId is null on a line written before variants, and Postgres
        // otherwise treats every null as different - which would let one product be added twice by an
        // older client and break the very guarantee this index exists for.
        builder.HasIndex(l => new { l.CartId, l.ProductId, l.VariantId })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
