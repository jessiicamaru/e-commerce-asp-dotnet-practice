using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShipTo_City",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo_Country",
                table: "orders",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo_Line1",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo_Line2",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo_Phone",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo_PostalCode",
                table: "orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo_RecipientName",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo_Region",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingOptionCode",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingOptionName",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingPrice",
                table: "orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingReference",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // A successful checkout now reads Paid rather than Completed (specs/011 research D3). Existing
            // rows are rewritten so the fulfilment queue sees them. Safe in both directions: an image from
            // before this change already knows "Paid", and this image reads a stray "Completed" as Paid.
            // Deliberately not reversed in Down - a Paid order is still correct under the old image.
            migrationBuilder.Sql("UPDATE orders SET \"Status\" = 'Paid' WHERE \"Status\" = 'Completed';");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_CreatedAt",
                table: "orders",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_Status_CreatedAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShipTo_City",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShipTo_Country",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShipTo_Line1",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShipTo_Line2",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShipTo_Phone",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShipTo_PostalCode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShipTo_RecipientName",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShipTo_Region",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingOptionCode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingOptionName",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ShippingPrice",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TrackingReference",
                table: "orders");
        }
    }
}
