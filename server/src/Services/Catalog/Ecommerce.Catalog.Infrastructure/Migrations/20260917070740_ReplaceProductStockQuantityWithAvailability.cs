using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceProductStockQuantityWithAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockQuantity",
                table: "products");

            migrationBuilder.AddColumn<bool>(
                name: "Availability",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityObservedAt",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Availability",
                table: "products");

            migrationBuilder.DropColumn(
                name: "AvailabilityObservedAt",
                table: "products");

            migrationBuilder.AddColumn<int>(
                name: "StockQuantity",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
