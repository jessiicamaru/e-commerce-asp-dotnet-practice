# seed

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
