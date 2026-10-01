# Contracts: Staff see a deleted account as deleted

`GET /api/users?search=&page=&pageSize=&includeDeleted=` - new optional `includeDeleted` (default false). `UserAdminResponse` gains `deletedAt: string | null`. Moderation commands on a deleted account: **409** ProblemDetails with `code: "AccountDeleted"`.
