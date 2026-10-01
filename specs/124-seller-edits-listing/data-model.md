# Data Model: A seller edits what they listed

No migration. `ProductResponse` gains `original: { name, description } | null` and `translations: { language, name, description }[] | null` - filled on the lookup, null on every list.
