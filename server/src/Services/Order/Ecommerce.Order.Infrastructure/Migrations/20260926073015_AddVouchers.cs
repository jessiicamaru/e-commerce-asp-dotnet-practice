using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_no_discount_yet",
                table: "orders");

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformDiscount",
                table: "order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShopDiscount",
                table: "order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "voucher_customer_uses",
                columns: table => new
                {
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Uses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_customer_uses", x => new { x.VoucherId, x.CustomerId });
                    table.CheckConstraint("CK_voucher_customer_uses_uses", "\"Uses\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Benefit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TotalLimit = table.Column<int>(type: "integer", nullable: true),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    PerCustomerLimit = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vouchers", x => x.Id);
                    table.CheckConstraint("CK_vouchers_used_count", "\"UsedCount\" >= 0 AND (\"TotalLimit\" IS NULL OR \"UsedCount\" <= \"TotalLimit\")");
                });

            migrationBuilder.CreateTable(
                name: "voucher_amounts",
                columns: table => new
                {
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FixedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinSubtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_amounts", x => new { x.VoucherId, x.Currency });
                    table.ForeignKey(
                        name: "FK_voucher_amounts_vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voucher_conditions",
                columns: table => new
                {
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_conditions", x => new { x.VoucherId, x.Type });
                    table.ForeignKey(
                        name: "FK_voucher_conditions_vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voucher_redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Benefit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voucher_redemptions_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_voucher_redemptions_vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_targets",
                columns: table => new
                {
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_targets", x => new { x.VoucherId, x.Type, x.TargetId });
                    table.ForeignKey(
                        name: "FK_voucher_targets_vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_discount_not_negative",
                table: "orders",
                sql: "\"DiscountTotal\" IS NULL OR \"DiscountTotal\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_order_items_discounts",
                table: "order_items",
                sql: "\"ShopDiscount\" >= 0 AND \"PlatformDiscount\" >= 0 AND \"ShopDiscount\" + \"PlatformDiscount\" <= \"UnitPrice\" * \"Quantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_OrderId",
                table: "voucher_redemptions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_VoucherId_OrderId",
                table: "voucher_redemptions",
                columns: new[] { "VoucherId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_Code",
                table: "vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_SellerId_CreatedAt",
                table: "vouchers",
                columns: new[] { "SellerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_amounts");

            migrationBuilder.DropTable(
                name: "voucher_conditions");

            migrationBuilder.DropTable(
                name: "voucher_customer_uses");

            migrationBuilder.DropTable(
                name: "voucher_redemptions");

            migrationBuilder.DropTable(
                name: "voucher_targets");

            migrationBuilder.DropTable(
                name: "vouchers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_discount_not_negative",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_order_items_discounts",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "PlatformDiscount",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "ShopDiscount",
                table: "order_items");

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_no_discount_yet",
                table: "orders",
                sql: "\"DiscountTotal\" IS NULL OR \"DiscountTotal\" = 0");
        }
    }
}
