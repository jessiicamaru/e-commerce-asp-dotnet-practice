# Research: Security headers on both apps

## D1. Where the headers live

**Decision**: one snippet, `client/nginx/security-headers.conf`, copied to `/etc/nginx/snippets/` and included in
every location of `default.conf.template`. HSTS goes in the Caddyfile.

**Rationale**:
- **Why every location**: nginx inherits `add_header` from the server block only into locations that set none of
  their own, and every location here sets `Cache-Control`. A server-level header would therefore reach no page at all.
  The image check asks each kind of answer, so a location that forgets is caught.
- **Why a snippet**: both apps share the template, so they get the same headers with one copy to read.
- **Why HSTS at Caddy**: the apps' nginx speaks plain HTTP behind Caddy. HSTS on an HTTP response is ignored by
  browsers, and in development it would be wrong anyway.

**Alternatives rejected**:
- *`add_header_inherit`*: it needs nginx 1.29.3 or later, and the image is 1.27.
- *Headers in the gateway*: the gateway answers `/api`, not the pages. The pages are what a CSP protects.

## D2. `style-src 'self' 'unsafe-inline'`, and scripts strictly `'self'`

**Finding**: the first attempt was `style-src 'self'`. Every flow passed on the page, but the new fixture reported:

```text
http://portal.localhost:8089/: Applying inline style violates the following Content Security Policy directive
'style-src 'self''. ... a hash ('sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=') ...
http://portal.localhost:8089/: ... a hash ('sha256-StEaX+se6YS7pqjzrzMIA0KaX9zF/8zAhvQXZAe5epY=') ...
```

The page's only `<style>` was Sonner's: 14,916 characters, inserted empty at load and then filled. That is two
checks, which is why there are two hashes. Every toast was therefore unstyled, and no assertion could have seen it.
Searching both bundles for `createElement("style")` found three injectors:
1. Sonner, in both apps;
2. TipTap's `data-tiptap-style`, in the back office's email editor;
3. React's hoisted `<style>` support.

The sanitised HTML the apps render (notices through DOMPurify, email previews, the editor's own content) also carries
`style` attributes, which `style-src` governs too.

**Decision**: `style-src 'self' 'unsafe-inline'`. `script-src 'self'`, with no `'unsafe-inline'` and no
`'unsafe-eval'`.

**Rationale**: a CSP exists to stop injected script, and that half stays strict. Inline styles can do far less: their
known abuse is exfiltrating attribute values through selectors, which needs injected markup that the sanitisers
already refuse. The alternative would leave a policy that is strict only on the pages the tests visit. The exact
policy is pinned by the image check, so the allowance cannot widen silently.

**Alternatives rejected**:
- *Hashes for each injected style*: they would cover Sonner and TipTap at today's versions only. Every Dependabot bump
  would break them, the style attributes in sanitised HTML still need `'unsafe-inline'` (or `'unsafe-hashes'` per
  attribute value, which is not workable for user content), and React's hoisted styles have no fixed content.
- *Nonces*: a static nginx cannot mint a nonce per response and put it into a bundle built in advance.
- *TipTap's `injectCSS: false`*: it fixes one of the three injectors.

## D3. Cross-origin isolation: COOP, COEP, CORP

**Decision**: `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Embedder-Policy: require-corp`,
`Cross-Origin-Resource-Policy: same-origin`, on everything both apps serve, `/api` included.

**Rationale**:
- Everything each app loads is its own origin, so COEP costs nothing today.
- COOP cuts the window reference between the shop and a page it opens. That includes the VNPay gateway and the other
  app.
- Every flow passes under them, including:
  - crossing from the storefront to the back office with a handoff;
  - the VNPay round trip;
  - uploading a photograph, whose preview is `blob:` and same-origin.
- No email embeds a product image, so CORP on `/api` blocks no legitimate cross-site load.

**Trade-off**: a future cross-origin subresource, such as a CDN for images, must send CORP or COEP has to be revisited.
The fixture reports `ERR_BLOCKED_BY_RESPONSE`, so that would surface in the first browser run, not in production.

## D4. The rest

| Header | Value | Why |
| :-- | :-- | :-- |
| `X-Content-Type-Options` | `nosniff` | a response is what its type says, never sniffed into a script |
| `X-Frame-Options` | `DENY` | `frame-ancestors 'none'` for browsers that predate it |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | another site learns the origin, never the path: `/orders/<id>` stays private |
| `Permissions-Policy` | camera, microphone, geolocation, payment, USB and the motion sensors `()` | nothing uses them, so a script that slips in cannot either |
| `server_tokens off` | `Server: nginx` | a version tells a scanner which advisories to try |
| HSTS (Caddy) | `max-age=31536000` | one year. Not `includeSubDomains` or `preload`: the shop does not own every name under its domain |

## D5. Holding the line

Each check was shown failing on a planted gap:
- **The image check** asserts all eight headers on five kinds of answer (`/`, a deep link, a hashed asset,
  `/app-config.js`, `/api`) and the CSP character for character. With the include removed from the `/assets/`
  location, it failed on that one answer, naming every missing header.
- **The Playwright fixture** collects console messages naming a CSP, COEP or CORP policy, and requests failed with
  `BLOCKED_BY_RESPONSE` or `BLOCKED_BY_CSP`. It is checked after every test. Its first run against `style-src 'self'`
  failed two tests, naming Sonner's style: that run is the fixture's negative control.
- **ZAP's rules** mark the six header findings FAIL from now on. 10055 (`style-src 'unsafe-inline'`) is WARN, with D2
  as its reason.
