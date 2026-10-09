# Research: Search suggestions while typing

## D1 - Products come from the listing's search

**Decision**: the suggestion handler asks `GetPaginatedAsync` with the term as `searchTerm`, page size 6.

**Rationale**: the search already ignores diacritics on both sides, covers the translation and the original, is indexed
(`f_unaccent` under trigram GIN indexes, specs/074 - 1.2 ms on 100,000 products) and keeps only what is on the shelf. A
second matcher would disagree with the results page the shopper lands on after Enter.

## D2 - Categories are matched in memory

**Decision**: the active categories are read (the same read the categories endpoint makes) and matched in C# against
their name in the reader's language and their original name, both folded: lower-case, diacritics removed, `đ` to `d`.

**Rationale**: a shop has tens of categories; an index for them would be ceremony. The fold mirrors `f_unaccent`'s
effect for Vietnamese, which is what matters here.

## D3 - One answer, two lists, small

**Decision**: `{ products: [{ id, name, imageUrl, price, currency, priceVaries }], categories: [{ id, name }] }`.

**Rationale**: a dropdown needs a picture, a name and a price - not variants, specifications or a seller. A small answer
keeps typing quick; the full product loads when it is opened.

## D4 - The client debounces and keeps the newest answer

**Decision**: 200 ms after the last key, one request keyed by the term (TanStack Query); the dropdown shows the answer
for the term currently typed only.

**Rationale**: keyed by the term, an older answer arriving late cannot overwrite a newer one - it is cached under its
own key.

## D5 - A hand-written combobox, not the kit's

**Decision**: the input gets `role="combobox"`, `aria-expanded`, `aria-controls`, `aria-activedescendant`; the list is
`role="listbox"` with `role="option"` items; mouse-down on an option does not blur the input.

**Rationale**: the kit's base-ui Combobox chooses from a fixed list and owns the input's value; here the input is a free
search whose Enter must keep its meaning. The ARIA pattern is small enough to write once.
