# Contracts: Automated security scanning in CI

No HTTP, message or gRPC change. The contracts are the workflows' triggers, permissions and failure conditions:

| Workflow / step | Triggers | Permissions | Fails when |
| :-- | :-- | :-- | :-- |
| `codeql.yml` | pull request to main, push to main, weekly (Monday) | `contents: read`, `security-events: write`, `actions: read` | the analysis cannot run. Alerts appear in code scanning; GitHub can block a pull request on them through a ruleset, which is the owner's choice |
| `ci.yml` build: vulnerable NuGet | as CI | `contents: read` | `dotnet list package --vulnerable --include-transitive` lists any package |
| `ci.yml` client: npm audit | as CI | `contents: read` | a runtime package has a high or critical advisory |
| `zap.yml` | push to main, `workflow_dispatch` | `contents: read` | a rule marked FAIL in `.zap/rules.tsv` is raised |
| `dependabot.yml` | weekly | Dependabot's own | never; it opens pull requests |
