# Quickstart: Validating category management

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Scenario 1 - The tests (SC-001, SC-002)

```bash
cd server
DB_PASSWORD=... SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~Category"
cd ../client
npx vitest run src/pages/admin-categories
```

**Expected**: green - `CategoryAdminTests` (4) and the page's 7 (including `slugOf`).

## Scenario 2 - Through the gateway (SC-003)

Bruno's `category/` folder: rename 200 with the slug kept, a customer's rename 403, a duplicate address 409.

## Scenario 3 - The page

As the administrator, open "Categories" in the console menu (`/admin/categories`): type "Đèn flash" - the address reads
`den-flash` - and create; edit it, add "Flashes" in English, save; delete it. Try deleting a category with products:
the refusal is shown in its words.

## Scenario 4 - Mutations (SC-004)

| Mutation | Expected red |
| :-- | :-- |
| The rename not saved (`category.Name` not set) | `A_rename_changes_the_default_text_keeps_the_slug_and_is_on_the_record` |
| The duplicate slug back to `new Exception(...)` | `A_slug_already_taken_is_a_conflict_not_a_server_error` |
| The page always translating, never removing an emptied English | "removes the English when its name is emptied" |
