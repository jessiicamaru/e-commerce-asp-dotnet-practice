using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TrackingReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_shipments_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_shipments_OrderId_SellerId",
                table: "order_shipments",
                columns: new[] { "OrderId", "SellerId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_order_shipments_SellerId",
                table: "order_shipments",
                column: "SellerId");

            // Backfill (specs/035 research D3): every existing order gets one part per seller it holds
            // goods from - and one for the shop's own goods - in the state the ORDER is in. An order an
            // administrator shipped before this is a shipped part now, with the same tracking reference.
            //
            // Written out here rather than shared with OrderRepository.EnsureShipmentsAsync on purpose:
            // a migration is a record of what ran, and must not change when that code does. The two
            // statements are the same rule; ShipmentTests pins the repository's.
            migrationBuilder.Sql("""
                INSERT INTO order_shipments ("Id", "OrderId", "SellerId", "Status", "TrackingReference", "UpdatedAt")
                SELECT gen_random_uuid(),
                       o."Id",
                       i."SellerId",
                       CASE o."Status" WHEN 'Preparing' THEN 'Preparing' WHEN 'Shipped' THEN 'Shipped' ELSE 'Pending' END,
                       CASE WHEN o."Status" = 'Shipped' THEN o."TrackingReference" END,
                       o."UpdatedAt"
                  FROM orders o
                  JOIN (SELECT DISTINCT "OrderId", "SellerId" FROM order_items) i ON i."OrderId" = o."Id"
                ON CONFLICT ("OrderId", "SellerId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_shipments");
        }
    }
}
