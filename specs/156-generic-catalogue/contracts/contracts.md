# Contracts: A shop for anything, not only cameras

No HTTP, message or gRPC change. The seed calls the same endpoints as before (categories, products, variants,
translations, prices, stock).

Command line:

```bash
python seed/catalogue.py                  # validates every file in seed/catalogue/; exit 1 naming each problem
python seed/seed-catalogue.py [name ...]  # every vertical, or only the named ones (file names without .json)
python seed/clean-test-debris.py [--yes]  # keeps whatever any vertical names
```
