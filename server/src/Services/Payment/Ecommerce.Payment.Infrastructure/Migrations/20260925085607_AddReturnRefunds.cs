using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Payment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_OrderId",
                table: "refunds");

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnId",
                table: "refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId",
                table: "refunds",
                column: "OrderId",
                unique: true,
                filter: "\"ReturnId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_ReturnId",
                table: "refunds",
                column: "ReturnId",
                unique: true,
                filter: "\"ReturnId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refunds_OrderId",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "IX_refunds_ReturnId",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "ReturnId",
                table: "refunds");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId",
                table: "refunds",
                column: "OrderId",
                unique: true);
        }
    }
}
