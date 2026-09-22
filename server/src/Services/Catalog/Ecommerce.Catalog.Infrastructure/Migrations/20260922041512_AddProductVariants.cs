using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <summary>
    /// The variant becomes the sellable unit (specs/020), and every product that exists gets exactly
    /// one variant - <b>with the product's own id</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// That id reuse is the point. Inventory's stock rows and reservations, Cart's lines and Order's
    /// lines all hold a product id today, in three other databases. Reusing the id makes all of them
    /// correct with no cross-service backfill, and an older image that still sends a product id is
    /// sending a valid variant id. Research D2 records the rejected alternative.
    /// </para>
    /// <para>
    /// Additive throughout: two new tables, nothing dropped, renamed or narrowed. <c>products.Price</c>
    /// and <c>products.Sku</c> stay, because an earlier Catalog image selects them.
    /// </para>
    /// </remarks>
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OptionSummary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Availability = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AvailabilityObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_variants_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variant_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variant_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_variant_options_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_ProductId",
                table: "product_variants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_Sku",
                table: "product_variants",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_variant_options_VariantId_Name",
                table: "variant_options",
                columns: new[] { "VariantId", "Name" },
                unique: true);

            // One variant per existing product, carrying its price and sku, and REUSING its id.
            // No options: a product sold in one shape has nothing to choose between.
            migrationBuilder.Sql(@"
INSERT INTO product_variants
    (""Id"", ""ProductId"", ""Sku"", ""Price"", ""OptionSummary"", ""IsActive"",
     ""Availability"", ""AvailabilityObservedAt"", ""CreatedAt"", ""UpdatedAt"")
SELECT ""Id"", ""Id"", ""Sku"", ""Price"", '', ""IsActive"",
       ""Availability"", ""AvailabilityObservedAt"", ""CreatedAt"", now()
FROM products;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "variant_options");

            migrationBuilder.DropTable(
                name: "product_variants");
        }
    }
}
