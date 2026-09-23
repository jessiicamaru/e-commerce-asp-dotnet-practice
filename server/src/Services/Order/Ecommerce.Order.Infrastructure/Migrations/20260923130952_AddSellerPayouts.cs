using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "orders",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Commission",
                table: "order_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GoodsTotal",
                table: "order_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayoutId",
                table: "order_shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingShare",
                table: "order_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PartCount = table.Column<int>(type: "integer", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payouts", x => x.Id);
                    table.CheckConstraint("CK_payouts_covers_something", "\"PartCount\" > 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_commission_rate_range",
                table: "orders",
                sql: "\"CommissionRate\" IS NULL OR (\"CommissionRate\" >= 0 AND \"CommissionRate\" < 1)");

            migrationBuilder.CreateIndex(
                name: "IX_order_shipments_PayoutId",
                table: "order_shipments",
                column: "PayoutId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_order_shipments_terms_all_or_none",
                table: "order_shipments",
                sql: "(\"GoodsTotal\" IS NULL AND \"Commission\" IS NULL AND \"ShippingShare\" IS NULL) OR (\"GoodsTotal\" IS NOT NULL AND \"Commission\" IS NOT NULL AND \"ShippingShare\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_payouts_SellerId_CreatedAt",
                table: "payouts",
                columns: new[] { "SellerId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_order_shipments_payouts_PayoutId",
                table: "order_shipments",
                column: "PayoutId",
                principalTable: "payouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_shipments_payouts_PayoutId",
                table: "order_shipments");

            migrationBuilder.DropTable(
                name: "payouts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_commission_rate_range",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_order_shipments_PayoutId",
                table: "order_shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_order_shipments_terms_all_or_none",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Commission",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "GoodsTotal",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "PayoutId",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "ShippingShare",
                table: "order_shipments");
        }
    }
}
