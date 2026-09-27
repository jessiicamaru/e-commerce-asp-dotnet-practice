using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLowStockThreshold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LowStockThreshold",
                table: "stock_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_items_low_stock_threshold_in_range",
                table: "stock_items",
                sql: "\"LowStockThreshold\" IS NULL OR \"LowStockThreshold\" BETWEEN 0 AND 100000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_items_low_stock_threshold_in_range",
                table: "stock_items");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "stock_items");
        }
    }
}
