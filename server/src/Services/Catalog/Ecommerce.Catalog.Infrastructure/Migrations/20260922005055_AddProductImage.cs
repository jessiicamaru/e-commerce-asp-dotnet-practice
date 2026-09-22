using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImageUpdatedAt",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_image_complete",
                table: "products",
                sql: "(\"ImageContentType\" IS NULL) = (\"ImageUpdatedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_image_type",
                table: "products",
                sql: "\"ImageContentType\" IS NULL OR \"ImageContentType\" IN ('image/jpeg', 'image/png', 'image/webp')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_products_image_complete",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_image_type",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ImageUpdatedAt",
                table: "products");
        }
    }
}
