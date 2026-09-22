# Feature Specification: Speaking More Than One Language

**Feature Branch**: not opened yet · **Created**: 2026-09-22 · **Status**: Specified, not built

**Input**: the owner asked for a spec for internationalisation, ahead of building it.

## Why this exists

The shop is written in English, in the code and in the database. The owner and the customers are
Vietnamese. Two different things are being asked for and they have almost nothing in common:

1. **The interface**: buttons, labels, messages. Text the shop owns and can translate once.
2. **The content**: product names and descriptions. Text that belongs to each product, differs per
   product, and has to be entered by whoever adds the product.

A single "add i18n" ticket would quietly mean the first and leave a Vietnamese shop with English
product names, which is the half that customers actually read.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The interface speaks the customer's language (Priority: P1)

A shopper opens the shop and sees Vietnamese; they can switch to English and back, and their choice
survives a reload.

**Why this priority**: it is the cheap half, and it makes the shop usable for the people it is for.

**Independent Test**: switch to Vietnamese, reload, and every label, button and message is Vietnamese.

**Acceptance Scenarios**:

1. **Given** a first visit, **When** the browser asks for Vietnamese, **Then** the interface is in
   Vietnamese without the shopper choosing anything.
2. **Given** any page, **When** the shopper switches language, **Then** every visible string changes,
   including validation messages the server sent.
3. **Given** a chosen language, **When** the shopper returns later, **Then** it is still chosen.
4. **Given** a string with no translation yet, **Then** the English text appears rather than a key or
   an empty space.

---

### User Story 2 - A product describes itself in each language (Priority: P1)

An administrator enters a product's name and description in Vietnamese and in English. A shopper sees
the one that matches their language.

**Why this priority**: the half that is actually read. Catalog owns this text, and nothing else can
translate it.

**Independent Test**: give a product a Vietnamese name, read the product in Vietnamese and in English,
and the two differ.

**Acceptance Scenarios**:

1. **Given** a product with both languages, **When** it is read in either, **Then** its own text comes
   back in that language.
2. **Given** a product translated into only one language, **When** it is read in the other, **Then**
   the default language's text is shown rather than nothing.
3. **Given** an option value (`Colour: Black`), **When** the shopper reads Vietnamese, **Then** it is
   Vietnamese too - options are read by customers as much as names are.

---

### User Story 3 - An order keeps the words it was bought with (Priority: P1)

An order shows what the customer saw at the time, in the language they bought in, however the
catalogue is edited afterwards.

**Why this priority**: an order is a record. This is the requirement that decides the whole design,
and the easiest to get wrong.

**Acceptance Scenarios**:

1. **Given** an order placed in Vietnamese, **When** it is read a year later in any language, **Then**
   the product name and options on it are the Vietnamese ones frozen at purchase.
2. **Given** an order placed before this feature, **Then** it still reads exactly as it does today.

---

### User Story 4 - Prices and dates read naturally (Priority: P2)

Money, dates and numbers are formatted the way the language expects.

**Acceptance Scenarios**:

1. **Given** Vietnamese, **When** a price is shown, **Then** it is grouped and suffixed as Vietnamese
   money is, not as English money is.
2. **Given** either language, **When** an order date is shown, **Then** it is in that language's
   conventions and in the shopper's own time zone.

### Edge Cases

- **A server error a customer sees**: RFC 7807 messages come from the services in English. Either the
  service translates them, or the client maps a known error to its own text. Decided below.
- **Search**: a Vietnamese search term must match Vietnamese product text, including without diacritics
  ("may anh" finding "máy ảnh").
- **An email or a receipt**: sent in the language the order was placed in, not the one the sender is
  reading.
- **A language the shop does not speak**: falls back to the default rather than 404.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shop supports Vietnamese and English. Vietnamese is the default; adding a third
  language must not need a schema change.
- **FR-002**: Every string the interface owns is translatable, and an untranslated one falls back to
  the default language rather than showing a key.
- **FR-003**: A product's name, description and option values are translatable per language.
- **FR-004**: A request says which language it wants; a client that says nothing gets the default.
- **FR-005**: An order freezes the words it was placed with, and reads back in those words for good.
- **FR-006**: Money, dates and numbers are formatted for the reader's language.
- **FR-007**: Search matches a term in the language it is written in, with or without diacritics.
- **FR-008**: Validation and error messages reaching a customer are in their language.
- **FR-009**: Nothing that exists today breaks: untranslated products keep their current text, and an
  earlier image still runs.

### Key Entities

- **Translation of a product**: a product, a language, and the text - name and description.
- **Translation of an option value**: the same for `Colour: Black`, which is a customer-facing string.
- **The chosen language**: per request, and remembered per visitor.

## Success Criteria *(mandatory)*

- **SC-001**: A Vietnamese shopper can browse, search, add to the cart, check out and read their order
  entirely in Vietnamese.
- **SC-002**: No visible string is English for a Vietnamese shopper except a product nobody has
  translated yet.
- **SC-003**: An order placed in Vietnamese still reads in Vietnamese after the catalogue is edited.
- **SC-004**: Adding a third language needs no migration, only rows and a translation file.

## Decisions to take before building *(these are the design, not details)*

### D1 - Where the language is decided

**Recommended**: the client sends `Accept-Language`; a signed-in customer's choice overrides it, and
the server never guesses from the country of the delivery address.

### D2 - How product text is stored

**Recommended**: a `product_translations` table (`ProductId`, `Language`, `Name`, `Description`), and
the same for option values. **Not** `NameVi`/`NameEn` columns, which need a migration per language, and
not JSON, which cannot be indexed for search per language.

The existing `products.Name`/`Description` stay as the default-language text, so the change is additive
and an earlier image keeps working.

### D3 - What an order freezes

**Recommended**: the order line already freezes the product name and the option summary (specs/009,
specs/020). It freezes them **in the language the order was placed in**, and the order stores that
language. This is the cheapest correct answer, and it is already half-built.

The rejected alternative — freezing every language, or re-reading the catalogue at display time — is
either bigger or breaks the record.

### D4 - Which side translates errors

**Recommended**: the client. Services keep returning English `ProblemDetails` with a stable
machine-readable shape (the field name in `errors`, the status), and the storefront turns a known error
into its own translated sentence. A service does not know who is reading.

The cost, recorded: an error the client does not recognise appears in English.

### D5 - Search with and without diacritics

**Recommended**: PostgreSQL `unaccent` plus a per-language index on the translated text. This is a real
piece of work and is the part most likely to be underestimated.

### D6 - What the interface uses

**Recommended**: `react-i18next` with JSON files per language, one namespace per area of the storefront
(`catalog`, `cart`, `checkout`, `orders`, `auth`). The language lives in the URL (`/vi/...`) or in a
stored preference — **decide before building**, because it changes every route.

## Assumptions

- Two languages to start, and no right-to-left language, which would change the layout as well as the
  text.
- One currency per price list (see the multi-currency feature); language and currency are **separate
  choices** — a Vietnamese speaker may want to pay in USD.
- No translation of what administrators see; the admin surface stays English.

## What this will cost, honestly

The interface half is a day's work. The content half is not: it changes the catalogue's schema, its
API, its search, its admin flow, and the seed data, and every product that exists has to be translated
by someone. The parts most often forgotten are **search** (D5) and **the frozen order words** (D3).
