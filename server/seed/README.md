# seed

## The catalogue

```bash
cd server
python seed/catalogue.py                                                          # checks every vertical
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... python seed/seed-catalogue.py            # seeds them all
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... python seed/seed-catalogue.py books      # or some
```

One JSON file per vertical in `catalogue/` (specs/156): cameras, electronics, fashion, home, books, sports. The shop
sells anything, and cameras were only its first example. A new vertical is a new file in the same format; the checker
(run by CI) refuses a duplicate SKU or slug across files, missing English text, an unknown category, variants of one
product naming different options, a dong price with a fraction or a dollar price with three decimals.
A category may name its department with `"parent": "<slug>"` (specs/158): the checker requires that department to be
declared by some loaded vertical and to have no parent itself, and the seeder creates departments first and moves a
category already there into its department. A category may declare `"specifications"` (code, vi, en, kind `Text`/`Choice`, options with code/vi/en) and a product
fills them in as `"specifications": {"<code>": "<option code or text>"}` (specs/159): the checker refuses an option or a
specification the product's category and department do not declare, and a category redeclaring its department's code;
the seeder declares them by code (idempotent) and sends each product's whole set. A variant may also carry
`compareAtVnd` / `compareAtUsd` (specs/161): what its price is compared against, checked to be above it and sent after
the price; four products carry one, about a fifth above the price, approximate like the prices. `clean-test-debris.py` keeps whatever
any file names. ⚠️ The prices are approximate - each file says so.

## Signing in as the administrator

With `ADMIN_TOTP_SECRET` set (development), Identity enrols the seeded administrator in two-factor sign-in at startup,
so the sign-in page asks for a code straight away. Get it, or the link to put the account in an authenticator app:

```bash
cd server
python seed/two_factor.py
```

## Product photographs

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/seed-images.py        # says what it would do
ADMIN_EMAIL=... ADMIN_PASSWORD=... python seed/seed-images.py --yes  # does it
```

Put one file per product in `seed/images/`, **named after the SKU** — `SONY-A7M4.jpg` finds the
product whose sku is `SONY-A7M4`. JPEG, PNG or WebP, at most 2 MB. The extension is ignored: the
type comes from the bytes, here and in Catalog (specs/019), so an SVG renamed `.png` is refused.

⚠️ **`seed/images/` is gitignored and must stay that way.** This repository is public and a product
photograph belongs to whoever took it. The files are staged there and uploaded to the local
`catalog_images` volume; nothing commits them. Where they came from and under what licence is
recorded in [IMAGE-CREDITS.md](IMAGE-CREDITS.md) — CC BY and CC BY-SA require attribution, and an
attribution nobody wrote down is not one.

The seeder **fetches nothing**. Choosing a source is a licensing decision, not a technical one, and
it belongs to whoever runs this rather than to the script.

Re-running replaces: Catalog writes the new file, switches the row, then deletes the old one, so a
second run never leaves a product pointing at a file that is gone. Verified — the volume held 14
files after replacing 11 of them, not 25.
