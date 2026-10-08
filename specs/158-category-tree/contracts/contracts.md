# Contracts: Categories in a tree - departments and their categories

## New: move a category

```http
PUT /api/categories/{id}/parent          Admin
{ "parentCategoryId": "0199...", }       or null for the top level

200  CategoryResponse                      the moved category
400  errors.ParentCategoryId               not found / itself / not a department / it has subcategories
401, 403                                   not signed in / not Admin
404                                        no such category
```

Audit: `Catalog` / `CategoryMoved`, before and after `{ ParentCategoryId }`.

## Changed

| Endpoint | Change |
| :-- | :-- |
| `POST /api/categories` | `parentCategoryId` is now checked: 400 when it is missing, or has a parent of its own |
| `DELETE /api/categories/{id}` | 409 when it has subcategories, naming how many |
| `GET /api/products?categoryId=` | includes products filed under the category's subcategories |

`CategoryResponse` is unchanged (it already carries `parentCategoryId`). No message or gRPC change.

## Seed format

A category may carry `"parent": "<slug of a department>"`.
