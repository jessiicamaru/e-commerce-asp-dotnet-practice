# Research: Dependabot proposes only what may be merged

## D1. Ignore the majors of MassTransit and PostgreSQL, not the packages

**Decision**: `ignore` with `update-types: ["version-update:semver-major"]` for `MassTransit*` and `postgres`.

**Rationale**:
- **MassTransit 9**: the version that needs a commercial license. 8.x patches remain welcome.
- **PostgreSQL 16 → 17/18**: a major version cannot start on the previous major's data directory. The upgrade is a
  dump and restore per database: eight databases, and a production volume. It is a planned change with its own record,
  never a bump that CI would pass on an empty test database while production's would refuse to start.

**Alternatives rejected**:
- *Ignore the packages entirely*: that would also drop security patches within the major.
- *Leave them and close each pull request*: Dependabot then stops proposing that version only. The next 9.x or 19 would
  open again.

## D2. Group the majors too

**Decision**: each ecosystem has two groups: `minor-and-patch` and `major`.

**Rationale**: 15 of the first run's 19 pull requests were single majors. One pull request per ecosystem for majors
is read in one sitting. When one package in it breaks the build, the owner splits that one out.
