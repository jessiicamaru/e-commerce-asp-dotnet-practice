# Data Model: Staff see a deleted account as deleted

No migration: `users.DeletedAt` exists since specs/112. The response gains `deletedAt` (nullable).
