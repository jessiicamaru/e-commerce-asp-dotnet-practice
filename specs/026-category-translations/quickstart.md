# Quickstart: Validating Category Translations

> Written on 2026-09-27, after the feature merged (#63), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

---

## Prerequisites

```bash
cd server
docker compose up -d
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ \
                          --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/   # applies AddCategoryTranslations
./start-dev.sh
```

An administrator token (`export ADMIN_EMAIL=... ADMIN_PASSWORD=...` on a line of its own first - the seed scripts read them too):

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

---

## Scenario 1 - The automated checks (FR-001 to FR-007, SC-003)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~CategoryTranslationTests"
```

**Expected**: 7 pass - a translated category reads in that language and says so; an untranslated one shows
its own text and says it is the default; the fallback is per field; writing twice replaces; removing
restores the category's own text; an unsupported language is refused; a missing category is 404.

**At the merge**: 262 tests across the six projects, 7 new.

---

## Scenario 2 - The seeded shop in both languages (User Stories 1 and 4, SC-001)

```bash
cd server
python seed/seed-catalogue.py     # writes the English names of the two categories
for lang in vi en; do
  curl -s "http://localhost:5000/api/categories?lang=$lang" | jq -r '.[] | "\(.name)  [\(.language)]  \(.description)"'
done
```

**Expected** (as the PR read it back from the running stack):

```
vi   Máy ảnh không gương lật  [vi]  Máy ảnh mirrorless, ống kính rời
en   Mirrorless cameras       [en]  Mirrorless bodies with interchangeable lenses
```

The storefront in English (language switcher) now shows English category names on cards and in the
filter, with no client change.

---

## Scenario 3 - Write, replace, fall back, remove (User Stories 2 and 3)

```bash
CAT=<a category id>
curl -s -X PUT http://localhost:5000/api/categories/$CAT/translations/en -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"name":"Test name"}' | jq '{name, description, language}'
# name "Test name", description = the category's own (per-field fallback), language "en"

curl -s -o /dev/null -w '%{http_code}\n' -X PUT http://localhost:5000/api/categories/$CAT/translations/fr \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"name":"Nom"}'
# 400 - the shop does not speak French

curl -s -o /dev/null -w '%{http_code}\n' -X DELETE http://localhost:5000/api/categories/$CAT/translations/en -H "Authorization: Bearer $ADMIN"
# 204; again: 204 (nothing to remove is not an error)
```

In Bruno, `category/translate category` (200, `language: "en"`) and
`category/category falls back to its own text` (asked in Vietnamese, the created text with
`language: "vi"`). **At the merge**: 87/87 requests, 130 tests.

---

## Scenario 4 - The table (data-model)

```sql
-- psql -h localhost -p 5433 -U $DB_USER ecommerce_catalog_db
SELECT "CategoryId", "Language", "Name" FROM category_translations;
-- one row per category per language; a second PUT in the same language leaves one row
```
