using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Orchestrator.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "order_state_data",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "order_state_data");
        }
    }
}
