using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTemplateVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_template_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Template = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BodyHtml = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_template_versions", x => x.Id);
                    table.CheckConstraint("CK_email_template_versions_words", "\"IsDefault\" OR (\"Subject\" IS NOT NULL AND \"BodyHtml\" IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_template_versions_Template_Language_Version",
                table: "email_template_versions",
                columns: new[] { "Template", "Language", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_template_versions");
        }
    }
}
