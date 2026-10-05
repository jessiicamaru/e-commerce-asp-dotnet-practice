# Quickstart: Automated security scanning in CI

```bash
# Dependencies, as CI checks them
cd server && dotnet list Ecommerce.slnx package --vulnerable --include-transitive
cd client && npm audit --omit=dev --audit-level=high

# The ZAP baseline against the running images (compose stack up)
docker run --rm --network server_edge -v "$PWD/.zap:/zap/wrk" ghcr.io/zaproxy/zaproxy:stable \
  zap-baseline.py -t http://storefront:8080 -c rules.tsv -I -r storefront.html
docker run --rm --network server_edge -v "$PWD/.zap:/zap/wrk" ghcr.io/zaproxy/zaproxy:stable \
  zap-baseline.py -t http://back-office:8080 -c rules.tsv -I -r back-office.html
```

Expected:
- no vulnerable NuGet package, and npm reports 0 vulnerabilities;
- ZAP: `FAIL-NEW: 0`, every WARN is one tracked by #294, and the rest are IGNOREd with a reason;
- on the pull request: the `CodeQL` checks for `csharp` and `javascript-typescript` pass, with their alerts under
  Security → Code scanning.

Negative controls:
- a planted vulnerable NuGet package (`System.Text.Json` 8.0.0) fails the build job's check;
- a planted runtime npm package with a high advisory fails the client job's audit.
