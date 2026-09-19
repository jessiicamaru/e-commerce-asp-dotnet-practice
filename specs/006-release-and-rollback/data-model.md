# Phase 1 Data Model: Releasable Versions, and Rollbacks That Survive

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-19

**No entity, no table, no migration.** Nothing in any database changes. Stated explicitly because a
feature whose subject is *schema changes* invites the assumption that it makes one.

Two things are modelled instead: how a version is named, and what makes a schema change breaking.
The second is the one worth arguing about.

---

## The identifier

```text
ghcr.io/jessiicamaru/ecommerce-<service>:sha-<short-sha>
```

| Part | Rule |
| :--- | :--- |
| `<service>` | `identity`, `catalog`, `order`, `orchestrator`, `inventory`, `payment`, `gateway` |
| `sha-<short-sha>` | The commit on `main` that produced it. **Never reused** |

| Tag | Deployable? | Purpose |
| :--- | :--- | :--- |
| `sha-<short-sha>` | **yes** | The only thing that may name a deployable version |
| `main` | **no** | Convenience for "the current one". It moves, so it cannot answer "the previous one" |

**The immutability is the feature.** A tag that can point at two different artifacts makes *"go back
to the version from before"* a sentence with no referent — which is the state this repository is in
today, where the only identifier is a commit somebody has to rebuild.

---

## Schema changes: the taxonomy the check and the rule share

The question that decides everything: **can the previous image still run against this schema?**

### Additive — the previous image survives

| Change | Why it is safe |
| :--- | :--- |
| Add a nullable column | Old code does not select it; new rows get `NULL` |
| Add a column with a default | Old code does not select it; existing rows get the default |
| Add a table | Nothing referenced it |
| Add an index | Invisible to a reader |
| Widen a type (`varchar(50)` → `varchar(200)`) | Every value the old code could read still fits |

**Most changes are additive, and they must stay cheap.** A rule that taxes the common case gets
worked around.

### Breaking — the previous image does not survive

| Change | What the old image does |
| :--- | :--- |
| **Drop a column** | `SELECT "X"` → the column does not exist → every query on that table fails |
| **Rename a column** | Same as dropping, with the added trap of looking like a rename rather than a removal |
| **Drop or rename a table** | Same, for everything that reads it |
| **Narrow a type** (`varchar(200)` → `varchar(50)`) | Reads still work; **writes** of existing-length values fail |
| **Add `NOT NULL` with no default** | Old code inserting without that column fails |

### The worked example, which is real

Released on 2026-09-17 in [#5](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/5):

```csharp
migrationBuilder.DropColumn(name: "StockQuantity", table: "products");
```

Any Catalog image built before that commit selects `products."StockQuantity"` on every product
query. Against the current schema it fails — so **rolling back Catalog past that commit takes the
catalogue down**, which is the opposite of what a rollback is for.

`Down` restores the column, but reverting a migration is not a rollback: it needs the database, it
needs the new code stopped first, and anything written since is lost. It is a recovery.

### The two-step shape

| Release | Does | Previous image survives? |
| :--- | :--- | :--- |
| **expand** | Add the new column; write both; read the new one, falling back to the old | **yes** — the old column is still there |
| **contract** | Drop the old column, once nothing deployed reads it | **yes** — nothing uses it any more |

The cost is two releases instead of one. The thing bought is that at no point does an older image
become unrunnable — which is the entire value of having kept those images.

---

## What the check can and cannot see

It reads a diff, not a database. That bounds it honestly:

| | |
| :--- | :--- |
| **Can see** | `DropColumn`, `DropTable`, `RenameColumn`, `RenameTable`, `AlterColumn` in added migration files |
| **Cannot see** | Whether an `AlterColumn` widens or narrows |
| **Cannot see** | Raw SQL in `migrationBuilder.Sql(...)` |
| **Cannot see** | Whether anything deployed actually reads the affected column |

So `AlterColumn` is reported as **may be breaking**, with the reason, rather than as a verdict. A
check that overstates gets ignored, and an ignored check is worse than an absent one because it
carries authority it has not earned.

The last row is the honest limit: the check can say *earlier images may break*, never *they will*. It
exists to make a reviewer look, not to decide.
