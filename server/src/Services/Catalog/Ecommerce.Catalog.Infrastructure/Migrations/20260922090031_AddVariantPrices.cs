using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "variant_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variant_prices", x => x.Id);
                    table.CheckConstraint("CK_variant_prices_amount", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_variant_prices_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_variant_prices_VariantId_Currency",
                table: "variant_prices",
                columns: new[] { "VariantId", "Currency" },
                unique: true);

            // ----------------------------------------------------------------------------------
            // The dollar list, seeded once, at 25,000 VND = 1 USD, on 2026-09-22.
            //
            // THIS RATE IS NOT A MECHANISM. It is written here and read nowhere: no code converts
            // between currencies at any point in a request (specs/022 research D2), and the rows this
            // produces are ordinary rows an administrator is expected to edit. Without a seed the shop
            // would come up selling nothing in dollars, which is correct behaviour and a useless
            // starting point.
            //
            // Rounded to 2 decimals because that is what a dollar has, and floored at 0.01 because a
            // price of 0.00 is a free camera, which the CHECK constraint (>= 0) would happily accept.
            //
            // No VND rows: `product_variants.Price` IS the default currency's price. Writing it twice
            // would create two sources for the same number, which is the defect this table exists to
            // avoid rather than to spread.
            // ----------------------------------------------------------------------------------
            migrationBuilder.Sql("""
                INSERT INTO variant_prices ("Id", "VariantId", "Currency", "Amount")
                SELECT gen_random_uuid(), v."Id", 'USD', GREATEST(ROUND(v."Price" / 25000.0, 2), 0.01)
                FROM product_variants v;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "variant_prices");
        }
    }
}
