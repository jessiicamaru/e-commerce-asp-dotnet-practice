# Security scanning

The code's own tests prove the rules this shop is built on: a seller's write to another's product is a 404, a token's
roles are checked by every service, a payment is recorded once. They cannot say whether a dependency has a published
vulnerability, whether a data-flow path leads user input somewhere dangerous, or what headers the server sends. Four
automated scans answer that (specs/150, #293). Design record: [specs/150-security-scanning](../../specs/150-security-scanning/).

## What runs, and when

| Scan | Sees | Runs | Fails the run when |
| :-- | :-- | :-- | :-- |
| **CodeQL** ([codeql.yml](../../.github/workflows/codeql.yml)) | data flow in C# (built) and TypeScript, `security-extended` queries | every pull request, main, weekly | the analysis cannot run. Alerts appear under Security → Code scanning |
| **Vulnerable NuGet packages** (build job) | every package, direct and transitive, against GitHub's advisories | every pull request | any package is listed |
| **Vulnerable npm packages** (client job) | runtime packages, the ones the bundle carries | every pull request | a high or critical advisory |
| **Dependabot** ([dependabot.yml](../../.github/dependabot.yml)) | newer versions: NuGet, npm, both Dockerfiles, compose images, Actions | weekly, grouped | never - it opens pull requests that CI tests |
| **OWASP ZAP baseline** ([zap.yml](../../.github/workflows/zap.yml)) | what the storefront's and back office's nginx images send a browser | main, pull requests touching nginx or the rules, on demand | a rule marked FAIL in [`.zap/rules.tsv`](../../.zap/rules.tsv) |

**Why npm counts runtime packages only.** The bundle is what runs in a shopper's browser. Build tools run on our own
machines, fed our own inputs, and Dependabot keeps them current without a gate that would be red for advisories that
cannot reach anyone (research D2).

**What Dependabot never proposes** (specs/153, #331). Each ecosystem gets at most two pull requests a week, one for
minor and patch updates and one for majors. Two majors are ignored:
- **MassTransit 9**: 8.x is Apache-2.0, while 9.x is under a commercial licence, so moving is the owner's licensing
  decision.
- **PostgreSQL 17/18**: a new major cannot start on the old data directory, so it is a planned dump and restore.

And since specs/155 (#353) **no MassTransit version at all**, patches included: 8.5.11 stopped consumers for good after
a broker outage, which CI cannot see, so a green pull request says nothing about a new version. It stays on 8.3.6
until a version passes `loadtest/fault.sh broker` several times. A vulnerable MassTransit still fails the build.

The first run proposed both (#315, #329, #330), and they were closed with that reason.

**Why ZAP is passive.** The baseline spiders the apps and judges the responses; it attacks nothing. An active scan
(injection, fuzzing) is for a person to run against a local stack, never in CI.

## How the ZAP rules are kept

Every rule that passed on 2026-10-05 is **FAIL**, so a regression fails the job. Each finding of the first round carries
a decision in the rules file's third column: **WARN** means known and tracked by the named issue, and **IGNORE** means
not applicable, with why. When #294 adds the security headers, their rules move from WARN to FAIL in the same change.

## The first round, 2026-10-05

### ZAP baseline (both apps)

58 rules passed and none failed. Nine findings, the same on both apps. **The six header findings were fixed by #294
(specs/151)** and are FAIL from then on. Fixing them raised one new finding, 10055 `style-src 'unsafe-inline'`, which is
WARN as an accepted trade-off (specs/151 research D2). Since #294: `FAIL-NEW: 0`, `WARN-NEW: 1`, 63 rules passing.

| Rule | Finding | Decision |
| :-- | :-- | :-- |
| 10020 | No anti-clickjacking header | **Fixed, #294**: `frame-ancestors 'none'`, `X-Frame-Options: DENY` |
| 10021 | No `X-Content-Type-Options: nosniff` | **Fixed, #294** |
| 10038 | No Content-Security-Policy | **Fixed, #294**: scripts `'self'` only |
| 10063 | No Permissions-Policy | **Fixed, #294** |
| 90004 | No Cross-Origin-Embedder-Policy | **Fixed, #294**: COOP, COEP `require-corp` and CORP. Everything the apps load is their own origin |
| 10036 | `Server: nginx/1.27.5` gives the version away | **Fixed, #294**: `server_tokens off` |
| 10055 | CSP: `style-src 'unsafe-inline'` (raised by the fix) | **Accepted**: Sonner and TipTap inject `<style>`, and sanitised HTML carries `style` attributes. Scripts stay `'self'`, and the image check pins the exact policy |
| 10027 | "Suspicious comments" | **Not applicable**: the matches are the words `admin` and `db` in minified code (a route pattern, a variable), not comments |
| 10049 | Storable and cacheable content | **Intended**: hashed `/assets/*` are cached for a year, while `index.html` and `/app-config.js` are `no-cache`, as `verify-storefront-image.sh` asserts |
| 10109 | Modern web application | **Informational**: a single-page app has no links for a spider; it says nothing about security |

### Dependencies

- **NuGet**: no vulnerable package, direct or transitive.
- **npm**: one advisory, reported as six "high" findings along its path: `braces` 3.0.3, stack exhaustion on deeply
  nested patterns.
  - It reaches us through `shadcn` → `fast-glob` → `micromatch` → `braces`.
  - **Not applicable**: `shadcn` is a build-time tool. The app imports only its CSS, which is compiled into the
    stylesheet, and its CLI runs on a developer's machine on our own patterns.
  - It moved to `devDependencies`, and the runtime audit is now clean.
  - `npm audit fix` offered `shadcn@1.0.0`, three majors back, so it was not taken. Dependabot will propose a fixed
    version when there is one.

### CodeQL

**No alerts** on the first analysis (pull request #309, 2026-10-05). C# ran 63 queries over the built solution and
TypeScript 103 queries, both at `security-extended`. The data-flow paths CodeQL looks for are the ones the codebase
already closes:
- every query is LINQ or parameterised SQL, and the `LIKE` escape is named (`SearchFunctions.Escape`);
- redirects are within the app;
- untrusted HTML goes through DOMPurify (`NoticeText`).

Zero is only worth reporting if the scan can find something, so a deliberate vulnerability was planted on the same
pull request and then reverted. CodeQL raised both:

| Planted | Alert |
| :-- | :-- |
| a Catalog action building `SELECT ... WHERE "Name" = '` + a query-string value + `'` | `cs/sql-injection`, high: SQL query built from user-controlled sources |
| a storefront function writing a URL parameter into `document.body.innerHTML` | `js/xss`, high: client-side cross-site scripting |

## Proving the gates bite

Each check was shown failing on something planted, then restored:

| Planted | Result |
| :-- | :-- |
| `System.Text.Json` 8.0.0 in a test project | listed with GHSA-hh2w-p6rv-4g7w and GHSA-8g4q-xg66-9fp4 (High): the build job's check fails |
| `lodash` 4.17.20 in the storefront | `npm audit --omit=dev --audit-level=high` exits 1, naming the command-injection advisory |
| ZAP rule 10021 switched to FAIL | `zap-baseline.py` exits 1 with `FAIL-NEW: 1` |
| a SQL injection and a DOM XSS (above) | CodeQL raises `cs/sql-injection` and `js/xss`, both high |

## Run them yourself

```bash
cd server && dotnet list Ecommerce.slnx package --vulnerable --include-transitive
cd client && npm audit --omit=dev --audit-level=high
# ZAP against the running images (compose stack up), from the repository root:
docker run --rm --network server_edge -v "$PWD/.zap:/zap/wrk" ghcr.io/zaproxy/zaproxy:stable \
  zap-baseline.py -t http://storefront:8080 -c rules.tsv -I -r storefront.html
```
