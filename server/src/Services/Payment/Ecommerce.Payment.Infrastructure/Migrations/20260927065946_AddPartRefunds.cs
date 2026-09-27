using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Payment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_OrderId",
                table: "refunds");

            migrationBuilder.AddColumn<Guid>(
                name: "PartId",
                table: "refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId",
                table: "refunds",
                column: "OrderId",
                unique: true,
                filter: "\"ReturnId\" IS NULL AND \"PartId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_PartId",
                table: "refunds",
                column: "PartId",
                unique: true,
                filter: "\"PartId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_OrderId",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "IX_refunds_PartId",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "PartId",
                table: "refunds");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId",
                table: "refunds",
                column: "OrderId",
                unique: true,
                filter: "\"ReturnId\" IS NULL");
        }
    }
}
