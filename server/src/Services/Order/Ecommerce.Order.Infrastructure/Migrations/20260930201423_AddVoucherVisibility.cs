using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "vouchers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "vouchers");
        }
    }
}
