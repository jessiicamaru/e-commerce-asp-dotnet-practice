using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopPauseAndClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "sellers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedBy",
                table: "sellers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "sellers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAt",
                table: "sellers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sellers_ClosedAt",
                table: "sellers",
                column: "ClosedAt",
                filter: "\"ClosedAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sellers_ClosedAt",
                table: "sellers");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "sellers");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "sellers");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "sellers");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "sellers");
        }
    }
}
