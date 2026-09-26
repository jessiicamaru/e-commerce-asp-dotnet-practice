# Tasks: Product images in object storage

- [ ] T001 Tests first, in `S3ProductImageStoreTests`, against SeaweedFS: the round trip, a missing key, a new key
  only, paging, the probe, unsafe keys, two instances, the orphan scan over the shared bucket, and the import.
- [ ] T002 `ProductImageKeys`, `S3ProductImageStore`, `SharedAcrossInstances`, the report's note,
  `ProductImageImport`, the store choice and its settings, and startup.
- [ ] T003 SeaweedFS in compose, the Catalog container on S3 with the old volume imported, `.env.example`, and CI.
- [ ] T004 A live check: two Catalog containers serve one image; the volume's images are in the bucket; the orphan
  report is clean; Bruno is green.
- [ ] T005 Mutation checks, the docs (catalog, CLAUDE.md, infrastructure, counts, timeline, backlog), and the
  reference.
