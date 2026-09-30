using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Activity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_notifications_ReadAt",
                table: "notifications",
                column: "ReadAt",
                filter: "\"ReadAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_ReadAt",
                table: "notifications");
        }
    }
}
