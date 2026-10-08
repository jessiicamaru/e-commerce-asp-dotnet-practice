using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "category_specifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_specifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_category_specifications_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "category_specification_translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_specification_translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_category_specification_translations_category_specifications~",
                        column: x => x.SpecificationId,
                        principalTable: "category_specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "specification_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specification_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_specification_options_category_specifications_Specification~",
                        column: x => x.SpecificationId,
                        principalTable: "category_specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_specifications",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_specifications", x => new { x.ProductId, x.SpecificationId });
                    table.CheckConstraint("CK_product_specifications_one_value", "(\"OptionId\" IS NOT NULL AND \"Text\" IS NULL) OR (\"OptionId\" IS NULL AND \"Text\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_product_specifications_category_specifications_Specificatio~",
                        column: x => x.SpecificationId,
                        principalTable: "category_specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_specifications_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_specifications_specification_options_OptionId",
                        column: x => x.OptionId,
                        principalTable: "specification_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "specification_option_translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specification_option_translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_specification_option_translations_specification_options_Opt~",
                        column: x => x.OptionId,
                        principalTable: "specification_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_category_specification_translations_SpecificationId_Language",
                table: "category_specification_translations",
                columns: new[] { "SpecificationId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_specifications_CategoryId_Code",
                table: "category_specifications",
                columns: new[] { "CategoryId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_specifications_OptionId",
                table: "product_specifications",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_product_specifications_SpecificationId",
                table: "product_specifications",
                column: "SpecificationId");

            migrationBuilder.CreateIndex(
                name: "IX_specification_option_translations_OptionId_Language",
                table: "specification_option_translations",
                columns: new[] { "OptionId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_specification_options_SpecificationId_Code",
                table: "specification_options",
                columns: new[] { "SpecificationId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_specification_translations");

            migrationBuilder.DropTable(
                name: "product_specifications");

            migrationBuilder.DropTable(
                name: "specification_option_translations");

            migrationBuilder.DropTable(
                name: "specification_options");

            migrationBuilder.DropTable(
                name: "category_specifications");
        }
    }
}
