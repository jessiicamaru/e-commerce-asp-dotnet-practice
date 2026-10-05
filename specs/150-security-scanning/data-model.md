# Data Model: Automated security scanning in CI

No table, column or migration. The only versioned data is the ZAP rules file. Each line holds a rule id, a decision and
a reason:

```text
.zap/rules.tsv
<rule id>\t<IGNORE | INFO | WARN | FAIL>\t<reason>
```

- **FAIL**: must never appear; it fails the job.
- **WARN**: known and tracked, with the issue named in the reason.
- **IGNORE**: not applicable, with the reason.

A finding moves from WARN to FAIL when its fix merges (#294's headers), so it cannot come back unnoticed.
