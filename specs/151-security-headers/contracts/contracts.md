# Contracts: Security headers on both apps

No API, message or gRPC change. The contract is the set of response headers in [data-model.md](../data-model.md),
held by three checks:

| Check | Asserts | Fails when |
| :-- | :-- | :-- |
| `verify-storefront-image.sh` (client job, both images) | all eight headers on `/`, a deep link, a hashed asset, `/app-config.js` and `/api`; the CSP exactly; no version in `Server` | a header is missing or different on any of them |
| Playwright `support/test.ts` (browser end-to-end job) | no CSP, COEP or CORP violation in any flow | the browser reports one |
| `zap.yml` with `.zap/rules.tsv` | ZAP's header rules pass | a rule marked FAIL is raised |
