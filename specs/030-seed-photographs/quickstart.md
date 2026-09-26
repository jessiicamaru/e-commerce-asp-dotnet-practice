# Quickstart: Validating the Photograph Seeder

> Written on 2026-09-27, after the feature merged (#69), from the code at that merge, the pull request and server/seed/README.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [README.md](./contracts/README.md)

There are no automated tests for this feature. These scenarios are how it was, and is, checked.

---

## Prerequisites

```bash
cd server
docker compose up -d
./start-dev.sh                                   # or the container overlay
export ADMIN_EMAIL=... ADMIN_PASSWORD=...        # on a line of its own
python seed/seed-catalogue.py                    # the 14 cameras
mkdir -p seed/images                             # then put <SKU>.jpg files in it - obtained and licensed by you
```

---

## Scenario 1 - Dry run, then upload (User Story 1, SC-001)

```bash
python seed/seed-images.py          # "14 image(s), 14 product(s)  (dry run - pass --yes to upload)"
python seed/seed-images.py --yes
```

**Expected** (as recorded in the PR):

```
14 image(s), 14 product(s)
  ok CANON-R50.jpg   -> Canon EOS R50        71KB, replaces the current image
  ...
  14 uploaded, 0 skipped, 0 failed
```

Open `http://localhost:5173/`: every card shows a photograph instead of the aperture tile.

---

## Scenario 2 - Re-running leaves one file per product (SC-002)

Run `python seed/seed-images.py --yes` again, then count files in the image store:

```bash
docker volume ls --format '{{.Name}}' | grep catalog_images            # the name compose gave it, e.g. server_catalog_images
docker run --rm -v server_catalog_images:/d alpine sh -c 'ls /d | wc -l'
```

**Expected**: one file per product. **At the merge**: 14 uploads over 11 existing images left 14 files, not
25 - independent confirmation that replacement deletes the old file (specs/029).

---

## Scenario 3 - Local refusals (User Story 2)

```bash
cp some-logo.svg seed/images/CANON-R8.png            # an SVG with a .png name
python seed/seed-images.py
```

**Expected**: `!! CANON-R8.png  not a JPEG, PNG or WebP by its bytes`, counted failed, exit code 1. A file
over 2 MB is reported as `<n>KB is over Catalog's 2MB limit`. A file named after no SKU is reported
`no product with sku <SKU>` and skipped. Whether these refusals were exercised at the merge is not
recorded.

---

## Scenario 4 - Nothing is committed (User Story 3, SC-003)

```bash
git check-ignore -v server/seed/images/CANON-R50.jpg     # .gitignore:<line>:server/seed/images/
git status --porcelain server/seed/                       # no image listed
```

Every SKU in `seed/cameras.json` has a row in `server/seed/IMAGE-CREDITS.md`.
