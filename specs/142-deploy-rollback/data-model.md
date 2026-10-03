# Data Model: Deploy and roll back from CI

No database table, column or migration. Services migrate on startup, as before.

## On the server

```text
$DEPLOY_PATH/                       default /opt/ecommerce
├── releases.log                    one line per outcome, appended, never rewritten
├── .lock                           flock while a deploy runs
└── releases/
    └── sha-1a2b3c4/                one directory per release that was shipped
        └── server/
            ├── docker-compose.yml, docker-compose.app.yml, docker-compose.prod.yml
            └── deploy/{Caddyfile, deploy.sh, production.env.example}
```

`releases.log` lines are `<UTC ISO time> <tag> <outcome>`, where the outcome is:

| Outcome | Meaning |
| :-- | :-- |
| `deployed` | healthy and the smoke checks passed |
| `failed` | the deploy did not become healthy, or a smoke check failed |
| `rolled-back` | after a failure, this earlier tag runs again, healthy |
| `rollback-failed` | the earlier tag did not come back either: someone must look now |

**The current release** is the newest `deployed` or `rolled-back` line. **`previous`** is the newest such line before
it that names a different tag.

## State transitions of one run

```text
name ok? ──no──► refused (exit 2, nothing touched)
  │yes
images published? ──no──► refused (exit 2, nothing touched)
  │yes
pull, up --wait, smoke ──ok──► "deployed" (exit 0)
  │fail
"failed" ──► previous exists? ──no──► exit 1
               │yes
             up --wait, smoke on previous ──ok──► "rolled-back" (exit 1)
               │fail
             "rollback-failed" (exit 1)
```
