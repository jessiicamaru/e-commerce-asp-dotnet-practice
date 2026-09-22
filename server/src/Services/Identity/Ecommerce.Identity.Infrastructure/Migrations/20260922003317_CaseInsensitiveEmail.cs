using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Infrastructure.Migrations
{
    /// <summary>
    /// One account per mailbox, whatever the case the email was typed in (issue #49).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Additive.</b> A unique index on <c>lower("Email")</c> is added beside the existing
    /// case-sensitive one, which stays. An earlier image still runs: it looks emails up exactly, and a
    /// case-only duplicate it tries to insert is refused by this index, as a 500 rather than a 409.
    /// </para>
    /// <para>
    /// <b>Existing duplicates stop the migration instead of being merged.</b> Two accounts that differ
    /// only by case each have their own password, addresses and orders. Deciding which one survives is
    /// a decision about a person's data, so it is not made here. The error gives a count and no
    /// addresses, because it ends up in logs. How to resolve it is in docs/guides/troubleshooting.md.
    /// </para>
    /// </remarks>
    public partial class CaseInsensitiveEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    clashes integer;
BEGIN
    SELECT count(*) INTO clashes
    FROM (SELECT lower(""Email"") FROM users GROUP BY 1 HAVING count(*) > 1) AS d;

    IF clashes > 0 THEN
        RAISE EXCEPTION 'Cannot make emails case-insensitive: % email(s) belong to more than one account, differing only by case. Resolve them first - see docs/guides/troubleshooting.md, ""Accounts that differ only by email case"".', clashes;
    END IF;
END $$;");

            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_users_Email_lower"" ON users (lower(""Email""));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_users_Email_lower"";");
        }
    }
}
