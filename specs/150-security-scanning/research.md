# Research: Automated security scanning in CI

## D1. CodeQL, C# built by hand

**Decision**: `csharp` with `build-mode: manual`, using the same steps as the build job: setup .NET 10, then `dotnet
build server/Ecommerce.slnx`. `javascript-typescript` with `build-mode: none`. The `security-extended` query suite is
used for both.

**Rationale**: with a real build CodeQL sees generated code and the resolved types, which `build-mode: none`
approximates. The build already works in CI. `security-extended` adds the lower-precision security queries, which are
worth having for a thesis that must argue its security, and the first round is triaged by hand anyway.

**Alternatives rejected**:
- *`build-mode: none` for C#*: faster, but less precise on exactly the data-flow paths that matter.
- *A third-party SAST such as Semgrep*: CodeQL is GitHub's own, free for public repositories, and its alerts live where
  the pull requests are.

## D2. Vulnerable packages fail the pull request; Dependabot only proposes

**Decision**:
- The build job runs `dotnet list package --vulnerable --include-transitive` and fails if anything is listed.
- The client job runs `npm audit --omit=dev --audit-level=high`.

**Rationale**: Dependabot's alerts are a repository setting, they do not block a pull request, and they arrive after
the fact. A step in CI fails the change that introduces the package. Runtime npm packages only, because the bundle is
what a browser runs; build tooling runs on our own machines with our own inputs. High and above, because moderate
npm advisories are mostly regular-expression denial of service in build tooling and would make the gate noise.

**Alternatives rejected**:
- *Every npm package, any severity*: it fails today on `braces` inside the `shadcn` CLI, a dev tool fed only our own
  glob patterns (D4). A gate that is always red is ignored.
- *OWASP Dependency-Check*: slow (it downloads the whole NVD), duplicates what NuGet and npm advisories already give,
  and needs an API key to be practical.

## D3. ZAP baseline against the images, on main

**Decision**: `zaproxy/action-baseline` against `http://localhost:8088` (storefront) and `:8089` (back office), served
by their own nginx images in the compose stack started the way the browser end-to-end job starts it.
- Rules live in `.zap/rules.tsv`; run with `-I`, so only rules marked FAIL fail the job.
- `allow_issue_writing: false`, so ZAP never files issues by itself.
- The reports are uploaded as artifacts.

**Rationale**: what ZAP checks (headers, cache rules, cookies, banners) is decided by the nginx configuration in the
image, so the image is what must be scanned, not the Vite dev server. On main and on demand only, because starting
the whole stack costs about ten minutes, and the headers change rarely.

**Alternatives rejected**:
- *The full (active) scan*: it attacks the target (injections, fuzzing) and takes far longer. It is out of scope for
  CI, and a person runs it against a local stack when needed.
- *Scanning the gateway's API*: the baseline spider finds no links in a JSON API. An API scan needs an OpenAPI import
  and is a separate piece of work if wanted.

## D4. The first round's findings

**ZAP baseline, both apps, 2026-10-05**: 58 rules passed and 9 warned, the same nine on each app.

| Rule | Finding | Decision |
| :-- | :-- | :-- |
| 10020 | No anti-clickjacking header | **#294**: `X-Frame-Options`/`frame-ancestors` |
| 10021 | No `X-Content-Type-Options` | **#294** |
| 10038 | No Content-Security-Policy | **#294** |
| 10063 | No Permissions-Policy | **#294** |
| 90004 | No Cross-Origin-Embedder-Policy | **#294** decides it. COEP `require-corp` breaks cross-origin images without CORP, so it may be recorded as not applicable there. |
| 10036 | `Server: nginx/1.27.5` | **#294**: `server_tokens off` |
| 10027 | "Suspicious comments" | **Not applicable**: the matches are the words `admin` and `db` in minified code (a route regex, a variable), not comments |
| 10049 | Storable and cacheable content | **Not applicable, intended**: hashed `/assets/*` cached for a year, `index.html` and `/app-config.js` `no-cache`, as `verify-storefront-image.sh` asserts |
| 10109 | Modern web application | **Informational**: it tells ZAP to use the AJAX spider, and says nothing about security |

**npm audit, 2026-10-05**: six "high" findings, which are one advisory: `braces` 3.0.3, stack exhaustion on deeply
nested patterns. It reaches us through `shadcn` → `fast-glob` → `micromatch` → `braces`.
- **Not applicable**: `shadcn` is a build-time tool. The app imports only its CSS, which is compiled into the
  stylesheet, and its CLI runs on a developer's machine on our own patterns.
- `npm audit fix` offers `shadcn@1.0.0`, a downgrade across three majors, so it is not taken.
- `shadcn` moves to `devDependencies`, where the runtime audit does not count it. Dependabot proposes the fixed
  version when one exists.

**NuGet, 2026-10-05**: `dotnet list package --vulnerable --include-transitive` lists nothing.

**CodeQL** (pull request #309): **no alerts** from 63 C# and 103 TypeScript `security-extended` queries. To show the
zero means something, a planted SQL injection (a Catalog action concatenating a query-string value into `CommandText`)
and a planted DOM XSS (a URL parameter into `innerHTML`) were pushed to the same pull request. CodeQL raised
`cs/sql-injection` and `js/xss`, both high, and the plant was reverted.

## D5. Dependabot, grouped and weekly

**Decision**: one entry per ecosystem and directory, run weekly. Minor and patch updates are grouped into one pull
request per ecosystem; majors come one at a time, since those are the ones that break. At most five open per
ecosystem.

| Ecosystem | Directories |
| :-- | :-- |
| `nuget` | `/server` |
| `npm` | `/client` |
| `docker` | `/server`, `/client` |
| `docker-compose` | `/server` |
| `github-actions` | `/` |

**Rationale**: about seventy ungrouped weekly pull requests would bury the work. Grouping is Dependabot's own answer.
