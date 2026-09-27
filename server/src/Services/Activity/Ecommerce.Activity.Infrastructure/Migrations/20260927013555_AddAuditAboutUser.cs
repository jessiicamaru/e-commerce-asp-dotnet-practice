using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Activity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAboutUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AboutUserId",
                table: "audit_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_AboutUserId_OccurredAt",
                table: "audit_entries",
                columns: new[] { "AboutUserId", "OccurredAt" });

            // An entry whose subject is a user is about that user - locks and bans from before this included, so a
            // moderator sees them (specs/100 D3). Entries about content cannot be backfilled: this database never
            // knew who wrote a review or sold a product.
            migrationBuilder.Sql("""
                UPDATE audit_entries SET "AboutUserId" = "SubjectId"::uuid
                 WHERE "SubjectType" = 'User'
                   AND "SubjectId" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_entries_AboutUserId_OccurredAt",
                table: "audit_entries");

            migrationBuilder.DropColumn(
                name: "AboutUserId",
                table: "audit_entries");
        }
    }
}
