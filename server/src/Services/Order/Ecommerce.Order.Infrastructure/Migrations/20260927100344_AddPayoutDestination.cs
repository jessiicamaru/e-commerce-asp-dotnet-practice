using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutDestination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaidToAccountLast4",
                table: "payouts",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidToBank",
                table: "payouts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidToHolder",
                table: "payouts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidToAccountLast4",
                table: "payouts");

            migrationBuilder.DropColumn(
                name: "PaidToBank",
                table: "payouts");

            migrationBuilder.DropColumn(
                name: "PaidToHolder",
                table: "payouts");
        }
    }
}
