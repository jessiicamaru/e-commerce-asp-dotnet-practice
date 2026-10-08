# Contracts: Product specifications per category

All through the gateway's existing `/api/categories/**` and `/api/products/**` routes.

## Declaring (Admin)

| Method | Path | Body | Answers |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/categories/{id}/specifications` | - | anyone: what applies to products in that category, department's first, in the reader's language |
| `POST` | `/api/categories/{id}/specifications` | `{ code, name, kind: "Text"|"Choice", options?: [{ code, value }] }` | 200 the specification; 400 bad kind/code/name, a choice with no options; 404 category; 409 code taken |
| `PUT` | `/api/categories/{id}/specifications/{specId}` | `{ name }` | 200; the default-language name |
| `PUT` | `/api/categories/{id}/specifications/{specId}/translations/{lang}` | `{ name }` | 200 |
| `DELETE` | `/api/categories/{id}/specifications/{specId}` | - | 204; 409 while products use it |
| `POST` | `/api/categories/{id}/specifications/{specId}/options` | `{ code, value }` | 200; 400 on a text specification; 409 code taken |
| `PUT` | `/api/categories/{id}/specifications/{specId}/options/{optionId}` | `{ value }` | 200 |
| `PUT` | `/api/categories/{id}/specifications/{specId}/options/{optionId}/translations/{lang}` | `{ value }` | 200 |
| `DELETE` | `/api/categories/{id}/specifications/{specId}/options/{optionId}` | - | 204; 409 while products use it |

```json
{ "id": "…", "categoryId": "…", "code": "brand", "name": "Brand", "kind": "Choice", "position": 0,
  "language": "en", "options": [ { "id": "…", "code": "apple", "value": "Apple", "language": "en" } ] }
```

## Filling in (Seller own, Admin)

`PUT /api/products/{id}/specifications` `{ "values": [ { "specificationId": "…", "optionId": "…" }, { "specificationId": "…", "text": "6.1 inch" } ] }`
→ 200 the product's specifications; 400 `values[n]` naming why; 404 not the caller's product.
Audit `Catalog` / `ProductSpecificationsSet`; a seller's approved product goes back to review (`ProductSentForReview`).

## Reading

- `GET /api/products/{id}` gains `specifications: [{ specificationId, name, kind, optionId, value }]` (applicable, with a value).
- `GET /api/products?optionIds=a&optionIds=b` - products having every option.

No message or gRPC change.
