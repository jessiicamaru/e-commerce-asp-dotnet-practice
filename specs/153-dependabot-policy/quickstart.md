# Quickstart: Dependabot proposes only what may be merged

```bash
python -c "import yaml; yaml.safe_load(open('.github/dependabot.yml'))"
gh pr list --author app/dependabot
```

Expected:
- the file parses;
- after the next weekly run, no pull request for MassTransit 9 or PostgreSQL 17/18, and at most two per ecosystem;
- Insights → Dependency graph → Dependabot shows no configuration error.
