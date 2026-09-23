using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "order_shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryConfirmedBy",
                table: "order_shipments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedAt",
                table: "order_shipments",
                type: "timestamp with time zone",
                nullable: true);

            // A part already shipped shipped when it last changed - nothing moves a part after it ships
            // (research D2). Without this, parcels shipped before today would never be auto-confirmed.
            migrationBuilder.Sql(
                "UPDATE order_shipments SET \"ShippedAt\" = \"UpdatedAt\" WHERE \"Status\" = 'Shipped' AND \"ShippedAt\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_order_shipments_Status_DeliveredAt_ShippedAt",
                table: "order_shipments",
                columns: new[] { "Status", "DeliveredAt", "ShippedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_shipments_Status_DeliveredAt_ShippedAt",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "DeliveryConfirmedBy",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "ShippedAt",
                table: "order_shipments");
        }
    }
}
