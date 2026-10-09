using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompareAtPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CompareAtAmount",
                table: "variant_prices",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompareAtPrice",
                table: "product_variants",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_variant_prices_CompareAtAmount",
                table: "variant_prices",
                sql: "\"CompareAtAmount\" IS NULL OR \"CompareAtAmount\" > \"Amount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_variants_CompareAtPrice",
                table: "product_variants",
                sql: "\"CompareAtPrice\" IS NULL OR \"CompareAtPrice\" > \"Price\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_variant_prices_CompareAtAmount",
                table: "variant_prices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_variants_CompareAtPrice",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "CompareAtAmount",
                table: "variant_prices");

            migrationBuilder.DropColumn(
                name: "CompareAtPrice",
                table: "product_variants");
        }
    }
}
