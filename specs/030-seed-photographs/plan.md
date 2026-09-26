# Implementation Plan: Seed Photographs

> Written on 2026-09-27, after the feature merged (#69), from the code at that merge, the pull request and server/seed/README.md.

**Branch**: `030-product-photographs` | **Date**: 2026-09-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/030-seed-photographs/spec.md`

## Summary

`server/seed/seed-images.py` signs in as an administrator through the gateway, reads the catalogue, matches
each file in `server/seed/images/` to a product by `file stem == SKU` (case-insensitive), checks the type
from the magic bytes and the 2 MB limit, and - only with `--yes` - uploads it as `multipart/form-data`
(part `file`) to `PUT /api/products/{id}/image`. `server/seed/images/` is added to `.gitignore`;
`server/seed/IMAGE-CREDITS.md` records each photograph's source, author and licence; `server/seed/README.md`
documents both. No server code changed.

## Technical Context

**Language/Version**: Python 3 (standard library only: `urllib`, `pathlib`, `uuid`, `json`)

**Primary Dependencies**: The running stack through the gateway: Identity's login, Catalog's product list
and image upload

**Storage**: None of its own. The images land wherever Catalog's `IProductImageStore` keeps them - at this
merge the `catalog_images` Docker volume (mounted at `/app/data`) or the `dotnet run` directory

**Testing**: No automated tests. Verified by running it against the stack

**Target Platform**: A developer machine with the stack up (Windows console included: output is forced to
UTF-8)

**Project Type**: Developer tooling

**Performance Goals**: None

**Constraints**: Fetch nothing; commit no image; refuse locally what Catalog would refuse; go through the
API, never SQL or the volume directly

**Scale/Scope**: 14 products, one image each

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The script writes to no database and no volume; every image goes through Catalog's own endpoint, so Catalog alone decides what is stored |
| **II. Clean Architecture Layering** | **Pass (not applicable).** No service code changed. The script duplicates Catalog's type and size checks only to refuse early; the server's checks remain the authority |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** No write of its own. Re-running relies on Catalog's write-switch-delete (specs/019, 029), which the run confirmed: 14 files after 14 uploads over 11 existing images |
| **IV. Identity Comes From the Token** | **Pass.** It signs in and sends the administrator's bearer token; the endpoint's `[Authorize(Roles = "Seller,Admin")]` decides. Credentials come from the environment, never a file in the repository |
| **V. Evidence Over Assumption** | **Pass.** Every photograph was looked at in a contact sheet before use, because two searches returned the wrong object; the file count on the volume was checked rather than assumed. The one image with no licence is labelled as such rather than passed off |

**Post-design re-check**: no violations. The licensing constraint is outside the constitution but is
treated with the same rule as secrets: it never enters the repository.

## Project Structure

### Documentation (this feature)

```text
specs/030-seed-photographs/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Five decisions with rejected alternatives
├── data-model.md        # No table changed
├── quickstart.md        # Validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── contracts/
│   └── README.md        # The endpoints relied on; none changed
└── tasks.md             # Reconstructed task list, all done
```

### Source Code (repository root, as changed by #69)

```text
.gitignore                    # + server/seed/images/
server/seed/seed-images.py    # new
server/seed/README.md         # new: how to run it, the licensing rule
server/seed/IMAGE-CREDITS.md  # new: source, author and licence per SKU
```

**Structure Decision**: Beside `seed-catalogue.py` and `clean-test-debris.py`, with the same shape: dry run
by default, `--yes` to act, `ADMIN_EMAIL`/`ADMIN_PASSWORD` from the environment, `GATEWAY_URL` defaulting
to `http://localhost:5000`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- `FUJI-X100VI` has no licensed photograph; its local placeholder must not be committed or published.
- `SONY-ZVE10M2` does not match the others' studio style.
- Nothing reproduces the photographs for another developer: each must obtain them and follow the credits.
- The script reads one page of 200 products.
- `IMAGE-CREDITS.md` opens with "Fetched with `seed/seed-images.py`", which the script does not do - it
  uploads what was fetched by hand. A wording slip in the committed file, left as it is.
- Since then: specs/079 moved the containers' images to an S3 bucket; the script is unchanged, since it only
  speaks to the endpoint.
