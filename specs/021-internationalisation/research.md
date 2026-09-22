# Research: Speaking More Than One Language

The spec (written first, [spec.md](spec.md)) named six decisions to take before building. This is what
they were settled as, and what building them turned up.

## D1 - Where the language is decided

**Decision**: a shared `IRequestLanguage` in `Ecommerce.Shared`, resolved per request in this order:

1. `?lang=` on the query string — one address, one language, so a link can be shared;
2. `Accept-Language`, matched loosely (`vi-VN` → `vi`);
3. the configured default (`Localization:DefaultLanguage`, `vi`).

A language the shop does not speak falls back to the default rather than 404 (FR-001).

**Rejected**: the country of the delivery address. Where a parcel goes says nothing about what its
buyer reads, and it would make the language change mid-checkout.

**Rejected**: a language in the path (`/vi/...`). It is the better answer for a public shop with SEO
to worry about; it also rewrites every route in the storefront, and this is a practice project. The
choice is a stored preference plus `?lang=` where a link needs to carry it. Recorded, not hidden.

## D2 - How product text is stored

**Decision**: `product_translations (ProductId, Language, Name, Description)` and
`variant_option_translations (OptionId, Language, Name, Value)`, each with a unique index on
`(<owner>, Language)`.

`products.Name`/`Description` and the option rows **stay**, holding the default-language text. They
are what an earlier image reads, they are the fallback when a translation is missing (FR-002), and
keeping them makes the change additive.

**Rejected**: `NameVi`/`NameEn` columns — a migration per language, which FR-001 forbids. **Rejected**:
JSON — cannot be indexed per language for search.

## D3 - What an order freezes

**Decision**: the order line already freezes the product name and option summary (specs/009,
specs/020). It now freezes them **in the language the order was placed in**, and `orders.Language`
records which that was.

**How the language reaches the words**: `PriceVariants` gains a `language` field, and Order passes the
request's language. That is additive to the proto, and an older Order sends nothing, which Catalog
reads as the default.

**Rejected**: freezing every language (an order is a record of one purchase, not a catalogue), and
re-reading the catalogue at display time (which is exactly what freezing exists to prevent).

## D4 - Which side translates errors

**Decision**: the client. Services keep answering English `ProblemDetails` with a stable shape - the
status and the field names in `errors` - and the storefront maps what it recognises onto its own
translated sentence.

**Why**: a service does not know who is reading. The gRPC and broker paths have no reader at all.

**The cost, recorded**: an error the storefront does not recognise appears in English. The storefront
already worded every error it shows, so this is the shape it was already in.

## D5 - Search with and without diacritics

**Decision**: `unaccent`, and **no index** for now.

Search matches the requested language's translation first and the default text as well, so a shop
half-translated still finds everything. `unaccent(lower(x)) LIKE unaccent(lower(term))` means "may
anh" finds "máy ảnh".

**The cost, recorded and measured**: `unaccent()` is not `IMMUTABLE`, so PostgreSQL will not build a
plain expression index on it; an index needs an `IMMUTABLE` wrapper function, and that is a deliberate
piece of DBA work rather than something to slip into a feature migration. Until then this is a
sequential scan over the catalogue. At 76 products it is invisible; at 100,000 it is the first thing
to fix. The spec called this the part most likely to be underestimated, and it was.

## D6 - What the interface uses

**Decision**: `react-i18next`, one JSON file per language under `client/src/locales/<lang>/<ns>.json`,
namespaces by area (`common`, `catalog`, `cart`, `checkout`, `orders`, `auth`). The chosen language
lives in `localStorage` and is sent as `Accept-Language` on every call, so the interface and the
product text agree without the page asking twice.

**Fallback**: `vi` → `en` → the key's default text, so an untranslated string shows English rather
than `catalog.addToCart` (FR-002).

## D7 - The default language is `vi`, and the existing text is what it is (found while building)

`products.Name` holds whatever an administrator typed - some of it English (`"Sony A7 IV"`), some
seeded by tests. Declaring the default `vi` does not make that text Vietnamese.

**Decision**: the default-language columns are "the text as entered", and the shop's default language
says which language a reader should assume it is in. A product with no Vietnamese translation shows
its original text; adding the translation is an editorial act, not a migration.

**Why this matters**: the alternative - marking existing text as English and requiring a Vietnamese
translation before a product appears - would empty the shop on upgrade.
