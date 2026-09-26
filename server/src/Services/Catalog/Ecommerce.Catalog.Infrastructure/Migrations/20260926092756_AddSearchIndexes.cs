using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Catalog.Infrastructure.Migrations
{
    /// <summary>
    /// Indexes the catalogue search (specs/074, #113). <c>unaccent()</c> is only STABLE, so an index over it was
    /// refused and every search scanned every product and translation. <c>f_unaccent</c> names the dictionary,
    /// which is what makes declaring it IMMUTABLE true - the standard pattern - and the trigram (<c>pg_trgm</c>)
    /// GIN indexes over it serve the query's <c>LIKE '%...%'</c>.
    /// </summary>
    /// <remarks>
    /// Additive: an earlier image's query (strpos over unaccent) still runs against this schema, just without the
    /// indexes. Down drops the indexes and the function and leaves both extensions, which other things may use.
    /// </remarks>
    public partial class AddSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION f_unaccent(text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                AS $$ SELECT unaccent('unaccent'::regdictionary, $1) $$;
                """);
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_products_name_search"
                    ON products USING gin (f_unaccent(lower("Name")) gin_trgm_ops);
                """);
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_products_sku_search"
                    ON products USING gin (lower("Sku") gin_trgm_ops);
                """);
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_product_translations_name_search"
                    ON product_translations USING gin (f_unaccent(lower("Name")) gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_product_translations_name_search";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_products_sku_search";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_products_name_search";""");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS f_unaccent(text);");
        }
    }
}
