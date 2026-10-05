# Data Model: Security headers on both apps

No table, column or migration. What changes is what every response from both apps carries:

| Header | Storefront and back office (nginx) | Production entrance (Caddy) |
| :-- | :-- | :-- |
| `Content-Security-Policy` | `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'` | passed through |
| `X-Content-Type-Options` | `nosniff` | passed through |
| `X-Frame-Options` | `DENY` | passed through |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | passed through |
| `Permissions-Policy` | `accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()` | passed through |
| `Cross-Origin-Opener-Policy` | `same-origin` | passed through |
| `Cross-Origin-Embedder-Policy` | `require-corp` | passed through |
| `Cross-Origin-Resource-Policy` | `same-origin` | passed through |
| `Server` | `nginx`, no version | removed |
| `Strict-Transport-Security` | not sent (plain HTTP) | `max-age=31536000` |
