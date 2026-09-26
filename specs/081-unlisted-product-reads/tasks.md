# Tasks: What hangs on a product off the shelf

- [X] T001 Tests in `UnlistedProductReadsTests`:
  - an image on sale is served with or without the key;
  - off the shelf it needs its own key;
  - a waiting product's photograph is shown to its seller and staff only;
  - a replacement retires the old key;
  - a variant's own photograph follows the same rule;
  - reviews and questions are a 404 but to the seller and staff, and everybody's while on sale.
- [X] T002 `ImageAccessKey` on products and variants, written by the image switch; addresses carrying it;
  `ProductImageKey.MayServe`; a private cache header off the shelf; `MaySee` on reviews and questions; the
  migration with its backfill.
- [X] T003 Bruno (a product taken down: its reviews and questions are a 404 to a stranger, and its seller still reads
  them), the live probe from the issue, five mutations, and the docs.
