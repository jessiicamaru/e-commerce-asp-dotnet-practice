# Data Model: Storefront image

> Written on 2026-09-27, after the feature merged (#133), from the code at that merge, the pull request and
> docs/infrastructure/running-in-containers.md.

**No table, column, index, migration or message changed.** The feature packages and publishes the storefront;
it stores nothing. The only state it has is configuration, listed in
[contracts/http-api.md](contracts/http-api.md): the `GATEWAY_URL` environment variable, read when the container
starts.
