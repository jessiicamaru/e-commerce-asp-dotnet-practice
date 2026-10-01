# Data Model: A seller edits what they listed

No migration. `ProductResponse` gains `translatedLanguages: string[] | null` (filled on the lookup, null on the listing).
