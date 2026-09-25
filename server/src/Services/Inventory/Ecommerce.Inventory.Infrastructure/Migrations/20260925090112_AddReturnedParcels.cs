using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnedParcels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "returned_parcels",
                columns: table => new
                {
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_returned_parcels", x => x.ReturnId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_returned_parcels_OrderId",
                table: "returned_parcels",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "returned_parcels");
        }
    }
}
