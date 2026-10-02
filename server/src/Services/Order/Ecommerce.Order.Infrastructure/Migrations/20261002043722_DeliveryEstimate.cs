using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryEstimate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxDays",
                table: "delivery_options",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinDays",
                table: "delivery_options",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_delivery_options_estimate",
                table: "delivery_options",
                sql: "(\"MinDays\" IS NULL AND \"MaxDays\" IS NULL) OR (\"MinDays\" IS NOT NULL AND \"MaxDays\" IS NOT NULL AND \"MinDays\" >= 0 AND \"MaxDays\" <= 60 AND \"MinDays\" <= \"MaxDays\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_delivery_options_estimate",
                table: "delivery_options");

            migrationBuilder.DropColumn(
                name: "MaxDays",
                table: "delivery_options");

            migrationBuilder.DropColumn(
                name: "MinDays",
                table: "delivery_options");
        }
    }
}
