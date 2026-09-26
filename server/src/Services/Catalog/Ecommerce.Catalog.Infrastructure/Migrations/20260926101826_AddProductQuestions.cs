using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    AskerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AskerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HiddenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HiddenReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HiddenBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AnsweredBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnswerUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnswerHiddenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnswerHiddenReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AnswerHiddenBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_questions_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_questions_CreatedAt",
                table: "product_questions",
                column: "CreatedAt",
                filter: "\"AnsweredAt\" IS NULL AND \"HiddenAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_questions_ProductId_CreatedAt",
                table: "product_questions",
                columns: new[] { "ProductId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_questions");
        }
    }
}
