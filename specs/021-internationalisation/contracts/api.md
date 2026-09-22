# Contracts: Speaking More Than One Language

## How a request says which language it wants

Every public read accepts, in order of precedence:

1. `?lang=vi` on the query string;
2. `Accept-Language: vi-VN,vi;q=0.9,en;q=0.8`;
3. the configured default (`vi`).

An unsupported tag falls back to the default. Responses carry `Content-Language`.

## Catalog

| Endpoint | Change |
| :-- | :-- |
| `GET /api/products`, `GET /api/products/{id}` | name, description and option values come back in the requested language, falling back to the default text |
| `GET /api/products?searchTerm=` | matches the requested language **and** the default text, ignoring diacritics: `may anh` finds `máy ảnh` |
| `PUT /api/products/{id}/translations/{lang}` — **Admin** | `{ name, description }` → 200. Creates or replaces that language's text |
| `DELETE /api/products/{id}/translations/{lang}` — **Admin** | 204; the product falls back to its default text |
| `PUT /api/products/{id}/variants/{variantId}/options/{optionId}/translations/{lang}` — **Admin** | `{ name, value }` → 200 |

`ProductResponse` gains `language` — which language the text in this response is in, after fallback.
It is what tells a client "this product has no Vietnamese yet".

## Order

`OrderDetailResponse` and `OrderResponse` gain `language`: the language the order was placed in, and
therefore the language of the words frozen on its lines.

`POST /api/orders` takes no new field — the request's language is used (D1).

## gRPC

`catalog_pricing.proto`:

```proto
message PriceVariantsRequest {
  repeated string variant_ids = 1;
  // Which language to freeze the words in (specs/021). Empty = the shop's default, which is what an
  // Order built before this feature sends.
  string language = 2;
}

message DescribeVariantsRequest {
  repeated string variant_ids = 1;
  string language = 2;
}
```

Additive: `PricedVariant` does not change shape — its `name` and `option_summary` are simply in the
requested language.

## Storefront

- A language switcher in the top bar; the choice is stored per visitor and sent as `Accept-Language`.
- Every visible string comes from `react-i18next`; an untranslated key falls back to English, never to
  the key itself.
- Money and dates are formatted for the chosen language.
