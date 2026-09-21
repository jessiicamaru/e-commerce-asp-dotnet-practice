using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountTotal",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "orders",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxTotal",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Orders placed before this feature were charged no tax: give them the parts that describe
            // what was actually charged - subtotal = total less delivery, tax 0, discount 0, rate 0 -
            // rather than inventing a tax that was never taken (specs/012 FR-011). Before the checks,
            // so the constraints are validated against rows that already satisfy them.
            migrationBuilder.Sql(@"
UPDATE orders
   SET ""Subtotal""      = ""TotalAmount"" - COALESCE(""ShippingPrice"", 0),
       ""TaxTotal""      = 0,
       ""DiscountTotal"" = 0,
       ""TaxRate""       = 0
 WHERE ""Subtotal"" IS NULL;
UPDATE order_items SET ""TaxAmount"" = 0 WHERE ""TaxAmount"" IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_no_discount_yet",
                table: "orders",
                sql: "\"DiscountTotal\" IS NULL OR \"DiscountTotal\" = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_parts_sum_to_total",
                table: "orders",
                sql: "\"Subtotal\" IS NULL OR \"Subtotal\" + COALESCE(\"ShippingPrice\", 0) + \"TaxTotal\" - \"DiscountTotal\" = \"TotalAmount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_tax_rate_range",
                table: "orders",
                sql: "\"TaxRate\" IS NULL OR (\"TaxRate\" >= 0 AND \"TaxRate\" < 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_no_discount_yet",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_parts_sum_to_total",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_tax_rate_range",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DiscountTotal",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TaxTotal",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "order_items");
        }
    }
}
