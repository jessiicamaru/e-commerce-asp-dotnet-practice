# Phase 0 Research: A Storefront That Looks Like a Shop

> Written on 2026-09-27, after the feature merged (#62), from the code at that merge, the pull request and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

No separate design record was written while the feature was built; these decisions are reconstructed
from the code, its comments and the pull request. The reference design itself is not in the repository
and is not described here beyond what the PR says of it.

---

## D1 - Theme tokens first, and our own palette

**Decision**: Rewrite the shadcn tokens in `client/src/index.css`: background `oklch(0.977 0.008 106)`
(warm off-white), cards pure white, a single saturated accent `--primary: oklch(0.885 0.175 124)` (lime)
with **dark** foreground `oklch(0.22 0.04 124)`, and `--radius: 1rem` (up from `0.625rem`). Dark-mode
tokens are rewritten in the same palette.

**Rationale**: Tokens change every component at once, so they change the most for the least. Pure white
behind white cards leaves nothing to separate them; warm paper lets a card lift off the page without a
border. A lime that bright cannot carry white text at any accessible contrast, hence dark text on it. The
radius is "the difference between a form control and an object you would pick up". The reference was
read for its language only: none of its branding is here.

**Alternatives considered**:

- **Restyle each component with its own classes.** Rejected: many edits for what one file does.
- **Copy the reference's palette and branding.** Rejected: it is not the shop's.
- **A white accent text on a darker green.** Not recorded as considered; the comment records only why
  white text on this lime fails.

---

## D2 - Everything in the hero is true

**Decision**: The hero shows an eyebrow and heading, a "browse everything" link, the product count, up to
six category chips, and one featured product with its real price. Nothing else.

**Rationale**: The reference carries "5m+ reviews" and "new gen" badges. This shop has no reviews and no
way to know what is new, so it says neither. "Invented social proof on a shop that has never sold
anything is the one thing a storefront must not do." Six chips because "a wall of chips is a filter, and
there is already a filter".

**Alternatives considered**:

- **Keep the reference's badges as decoration.** Rejected: they would be claims, and false ones.

---

## D3 - The hero appears on the landing view only, and features the dearest product

**Decision**: `landing = !searchTerm && !categoryId && pageNumber === 1`. The featured product is the
most expensive item on the page, and it sits **inside** the hero panel.

**Rationale**: Once somebody has searched, the results are what they came for and a hero is in the way.
Featuring `items[0]` put the same card in the hero and again as the first tile under it; placing the
featured card beside the panel left a hole where a product should be. Both were found only by looking.

**Alternatives considered**:

- **The first item.** Rejected after looking, for the repetition above.
- **A product marked "featured" by staff.** Rejected: no such field exists, and the storefront must not
  invent behaviour the API does not support.

---

## D4 - Search in the bar, writing the address the catalogue reads

**Decision**: A form in the top bar navigates to `/?q=<term>` (or `/` when empty). The bar shows the cart's
line count, asking for the cart only when signed in (`useCart(!!user)`).

**Rationale**: A shopper looking at one camera and wanting another should not have to go back first.
Reusing `?q=` keeps a search a shareable address with no second search state. A signed-out cart request is
a guaranteed 401.

**Alternatives considered**:

- **Search only on the catalogue page** (as before). Rejected for the reason above.
- **A separate search route.** Rejected: two addresses for one result list.

---

## D5 - A deliberate placeholder rather than a letter tile

**Decision**: With no image, or after an image fails to load, `ProductImage` draws a six-blade aperture on
a pale two-stop gradient whose hue is the sum of the id's character codes mod 360.

**Rationale**: The catalogue had no photographs and no honest way to obtain them in this feature. The tile
says "no photograph" in the shop's own visual language instead of looking broken, and a hue derived from
the id keeps one camera's tile the same everywhere while a grid reads as variety. The tint is kept pale so
it never competes with the price under it.

**Alternatives considered**:

- **The previous grey tile with the name's first letter.** Rejected: read as breakage.
- **Stock or scraped photographs.** Rejected: licensing; photographs came later, deliberately, in
  specs/030.

---

## D6 - Verify by looking, and believe the probe over the screenshot

**Decision**: With no browser tool available, drive the installed Chrome headless against the dev server,
read screenshots at 1440px, 500px and on the product page, and inject a probe when a screenshot suggests a
layout bug.

**Rationale**: Seven features had shipped without anybody clicking through the storefront. Looking found
four real defects: 95 junk categories in the filter, a hole in the hero, the featured product repeated,
and `← ← Back to the shop`. It also produced a false alarm: an apparent horizontal overflow at 430px was
"fixed" twice (`basis-full`, `min-w-0`) with comments explaining the bug. A probe reported
`VIEW=500 SCROLL=500` and no overflowing element: headless Chrome has a 500px minimum layout width and had
cropped the shot. Both fixes were reverted (one had made the bar a row taller) and the comments removed.
What the probe did surface was real - at 500px the bar wraps to four rows and `rounded-full` makes a tall
stadium - so the bar is `rounded-3xl` and only `sm:rounded-full`.

**Alternatives considered**:

- **Trust the screenshot.** What happened first, and it produced two changes for a bug that did not
  exist.
- **Claim phone widths.** Rejected: the tool cannot lay out below 500px, so the PR says "verified down to
  500px only".

---

## D7 - Delete a category only when empty, and let the API decide what the cleaner keeps

**Decision**: `DELETE /api/categories/{id}`, Admin only. `DeleteCategoryCommand` counts products filed
under the category and throws `ConflictException("'<name>' still has <n> product(s) filed under it. Move or
delete them first.")` when any are; otherwise it removes the row. The cleaner lists categories, and for
each whose slug `cameras.json` does not name it simply asks for deletion - a refusal means "keep it".

**Rationale**: A category shows up in the filter a shopper uses, so junk categories are worse than junk
products were. Deleting one that holds products would either orphan them or delete them, and neither is
what somebody tidying a taxonomy asked for. The `products.CategoryId` foreign key is `RESTRICT` and would
refuse anyway; checking first is how the caller gets a sentence instead of a constraint violation. Relying
on that refusal means the cleaner needs no cleverness about ordering.

**Alternatives considered**:

- **Cascade the products.** Rejected: silently destroys listings.
- **Move the products to a default category.** Rejected: nobody asked for a taxonomy change.
- **The cleaner decides which categories are empty.** Rejected: a second copy of a rule the API already
  enforces, and one that could be stale by the time it deletes.
