# Research: A seller edits what they listed

## D1 - One endpoint for the product's own text and category

**Decision**: `PUT /api/products/{id}` with name, description and category.

**Rationale**: They are the three fields of the creation form that describe the product rather than a shape of it. The default-language text lives in the product's own columns (specs/021), which no translation endpoint writes: a `vi` translation row would override them for Vietnamese readers while search, sorting and every other default read kept the old name.

**Alternatives rejected**: Writing the default text as a translation row - two copies of the Vietnamese name that disagree; three endpoints - three review moves for one edit.

## D2 - A category change goes back to review

**Decision**: Name, description **or category** changed by the seller of an approved product calls `AfterSellerEditAsync`.

**Rationale**: CLAUDE.md: a new edit of what a shopper reads must call it. A category decides which shoppers find the product and what it claims to be - a lens moved into Cameras is the kind of thing a moderator checks.

**Alternatives rejected**: Category exempt - an approved product could be moved anywhere unseen.

## D3 - Which translations exist

**Decision**: `translatedLanguages` on the lookup.

**Rationale**: A response's text falls back per field, so reading in English cannot tell "translated" from "falling back" - the page would offer to remove a translation that is not there, or prefill the Vietnamese as if it were English.

**Alternatives rejected**: A translations endpoint - one more route for a list of language codes.
