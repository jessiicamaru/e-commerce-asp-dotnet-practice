using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OptionSummary",
                table: "order_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "order_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                table: "order_items",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptionSummary",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "order_items");
        }
    }
}
