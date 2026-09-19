# Contract: Published Release Artifacts

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-19

What a merge to `main` leaves behind, and what may be relied on about it.

---

## What is published

Seven images, one per service, on every merge to `main` whose checks pass:

```text
ghcr.io/jessiicamaru/ecommerce-identity:sha-<short-sha>
ghcr.io/jessiicamaru/ecommerce-catalog:sha-<short-sha>
ghcr.io/jessiicamaru/ecommerce-order:sha-<short-sha>
ghcr.io/jessiicamaru/ecommerce-orchestrator:sha-<short-sha>
ghcr.io/jessiicamaru/ecommerce-inventory:sha-<short-sha>
ghcr.io/jessiicamaru/ecommerce-payment:sha-<short-sha>
ghcr.io/jessiicamaru/ecommerce-gateway:sha-<short-sha>
```

Plus a moving `:main` tag on each, for convenience.

---

## Guarantees

| Guarantee | What it means |
| :--- | :--- |
| **Immutable** | A `sha-` tag is written once. Pulling it in a year returns the same bytes |
| **Earned** | Published only after `build` (including all tests) and `auth-smoke` pass |
| **Scanned** | Each image is scanned for credentials **before** it is pushed. A failed scan pushes nothing |
| **Complete** | All seven, or none — a partial release is not a release |
| **Traceable** | The tag *is* the commit. No lookup table, no release notes to consult |

### `:main` is not deployable, and that is the point

It moves. Asking for "the previous `:main`" is not a question with an answer. Deployable versions are
named by `sha-` tags only (FR-004).

Use `:main` to try the current build; never to pin one.

---

## What is not guaranteed

- **That anything is running these images.** Nothing is deployed anywhere. These are artifacts, not
  a deployment.
- **That an older image works against the current database.** It does not, if a breaking schema
  change has been released since — see [schema-compatibility.md](./schema-compatibility.md). This is
  the half of rollback that images alone do not provide.
- **Any retention beyond "nothing deletes them".** Deliberate: the right rule keeps whatever is
  deployed, and nothing is (research D8).

---

## Using one

```bash
docker pull ghcr.io/jessiicamaru/ecommerce-catalog:sha-4fbfe28

docker run --rm -p 5057:8080 \
  -e DB_HOST=host.docker.internal -e CATALOG_DB_PORT=5433 \
  -e DB_PASSWORD=<password> -e JWT_SECRET=<secret> \
  ghcr.io/jessiicamaru/ecommerce-catalog:sha-4fbfe28
```

Configuration is unchanged from
[running-in-containers.md](../../../docs/infrastructure/running-in-containers.md) — these are the
same images `docker compose` builds, with a name.

To run the whole system from published images rather than building locally, override the `image:` of
each service in the compose overlay. No separate compose file ships for this: there is nowhere to
deploy, so a file describing a deployment would describe nothing.

---

## Ordering, and why it is not negotiable

```text
build all seven  →  scan each  →  push all seven
```

Not build-and-push-then-scan. Today `image-secrets` builds its own Catalog image and scans **that**;
once images are published, the scanned bytes and the shipped bytes would be different artifacts and
the guarantee would attach to the wrong one.

This repository is public. A `JWT_SECRET` in a published layer is readable by anyone, and a
credential that has been pulled must be **rotated**, not deleted — deleting the package does not
recall a copy. Scanning after pushing detects the problem after the damage.

---

## Permissions

The job declares `permissions: packages: write` and authenticates with the run-scoped
`GITHUB_TOKEN`. **No secret is stored for this**, which is what the constitution's Configuration rule
asks for: secrets are never written into a workflow file, and the best way to honour that is to need
none.
