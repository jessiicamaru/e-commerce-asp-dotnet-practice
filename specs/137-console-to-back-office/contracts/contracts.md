# Contracts: The admin and moderator console moves to the back office

No HTTP, message or gRPC change.

## Addresses

| Before (storefront) | After |
| :-- | :-- |
| `/admin` | back office `/` |
| `/admin/<page>[...]` | back office `/<page>[...]` - the storefront redirects |

## Run-time configuration (nginx, both images)

| Variable | Default in the image | Read by |
| :-- | :-- | :-- |
| `STOREFRONT_URL` | `http://localhost:8088` | the back office (product links) |
| `BACK_OFFICE_URL` | `http://portal.localhost:8089` | the storefront (redirects, staff links, notice links) |

`GET /app-config.js` → `window.__APP_CONFIG__ = { "storefrontUrl": "...", "backOfficeUrl": "..." }`, sent with
`Cache-Control: no-cache`.
