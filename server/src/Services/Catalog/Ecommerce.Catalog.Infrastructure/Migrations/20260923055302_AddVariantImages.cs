using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "product_variants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImageUpdatedAt",
                table: "product_variants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_variants_image_complete",
                table: "product_variants",
                sql: "(\"ImageContentType\" IS NULL) = (\"ImageUpdatedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_variants_image_type",
                table: "product_variants",
                sql: "\"ImageContentType\" IS NULL OR \"ImageContentType\" IN ('image/jpeg', 'image/png', 'image/webp')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_product_variants_image_complete",
                table: "product_variants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_variants_image_type",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "ImageUpdatedAt",
                table: "product_variants");
        }
    }
}
