using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImageAccessKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImageAccessKey",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageAccessKey",
                table: "product_variants",
                type: "uuid",
                nullable: true);

            // Every image already stored gets its key now (specs/081): without one, the image of a product off the
            // shelf could not be served even to its own seller. Only adds - an earlier image ignores the column.
            migrationBuilder.Sql("""UPDATE products SET "ImageAccessKey" = gen_random_uuid() WHERE "ImageUpdatedAt" IS NOT NULL;""");
            migrationBuilder.Sql("""UPDATE product_variants SET "ImageAccessKey" = gen_random_uuid() WHERE "ImageUpdatedAt" IS NOT NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageAccessKey",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ImageAccessKey",
                table: "product_variants");
        }
    }
}
