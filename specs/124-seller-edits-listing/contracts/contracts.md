# Contracts: A seller edits what they listed

`PUT /api/products/{id}` - Seller (own), Admin.

```json
{ "name": "Canon EOS R50", "description": "…", "categoryId": "…" }
```

200 `ProductResponse`; 400 (name 1-200, description ≤ 2000, an existing category); 404 not found or not yours.
Audit `Catalog` / `ProductDetailsEdited` with before and after. An approved product edited by its seller: `ReviewStatus`
`Pending`, off the shelf.

`GET /api/products/{id}` gains `translatedLanguages: ["en"]`.
