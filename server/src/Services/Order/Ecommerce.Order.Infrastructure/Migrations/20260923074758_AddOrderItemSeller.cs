using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemSeller : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_SellerId",
                table: "order_items",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_items_SellerId",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "order_items");
        }
    }
}
