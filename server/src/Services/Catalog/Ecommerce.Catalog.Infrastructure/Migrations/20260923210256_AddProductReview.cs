using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // Every product already listed was on sale before review existed, and an older image that
                // inserts without knowing this column must not create something unreadable (specs/045).
                defaultValue: "Approved");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_ReviewStatus_SubmittedAt",
                table: "products",
                columns: new[] { "ReviewStatus", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_ReviewStatus_SubmittedAt",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "products");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "products");
        }
    }
}
