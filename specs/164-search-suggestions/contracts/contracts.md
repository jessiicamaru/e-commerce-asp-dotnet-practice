# Contracts: Search suggestions while typing

## `GET /api/products/suggest?q=may%20anh` - anonymous, cached (specs/157)

```json
{
  "products": [
    { "id": "…", "name": "Sony Alpha A7 IV", "imageUrl": "/api/products/…/image?v=…", "price": 52000000, "currency": "VND", "priceVaries": true }
  ],
  "categories": [
    { "id": "…", "name": "Máy ảnh không gương lật" }
  ]
}
```

- Up to 6 products (on the shelf, matched by the catalogue's search) and up to 4 active categories (name in the reader's
  language or the original, case and diacritics ignored).
- In the request's language and currency, like every catalogue read.
- 400 when `q` is shorter than 2 or longer than 100 characters after trimming.
