using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Cart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartLineVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cart_lines_CartId_ProductId",
                table: "cart_lines");

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                table: "cart_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cart_lines_CartId_ProductId_VariantId",
                table: "cart_lines",
                columns: new[] { "CartId", "ProductId", "VariantId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cart_lines_CartId_ProductId_VariantId",
                table: "cart_lines");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "cart_lines");

            migrationBuilder.CreateIndex(
                name: "IX_cart_lines_CartId_ProductId",
                table: "cart_lines",
                columns: new[] { "CartId", "ProductId" },
                unique: true);
        }
    }
}
