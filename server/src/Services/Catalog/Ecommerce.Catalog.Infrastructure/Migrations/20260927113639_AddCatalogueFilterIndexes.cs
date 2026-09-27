using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogueFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_variant_prices_Currency_Amount",
                table: "variant_prices",
                columns: new[] { "Currency", "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_products_on_shelf_Price",
                table: "products",
                column: "Price",
                filter: "\"ReviewStatus\" = 'Approved' AND \"IsActive\" AND NOT \"SellerSuspended\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_variant_prices_Currency_Amount",
                table: "variant_prices");

            migrationBuilder.DropIndex(
                name: "IX_products_on_shelf_Price",
                table: "products");
        }
    }
}
