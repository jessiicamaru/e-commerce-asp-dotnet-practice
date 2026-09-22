# Storefront

A deliberately thin web client for this backend (issue [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)).
Its job is to exercise the API the way a person would and to surface what the backend lacks — not to
be polished.

**React 19 + TypeScript + Vite + Tailwind CSS v4 + shadcn/ui + axios + TanStack Query.** It talks
**only to the gateway**, under `/api`.

## Run it

```bash
# the backend first - everything behind the gateway on :5000
cd server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d

cd client
npm install
npm run dev          # http://localhost:5173 ; /api is proxied to http://localhost:5000
```

Point the proxy elsewhere with `GATEWAY_URL=http://host:port npm run dev`.

## Check it

```bash
npm run lint         # oxlint
npm run build        # tsc -b, then vite build - what CI runs
```

## Conventions

**A folder per thing, with an `index.tsx` (or `index.ts`) as its entry**, imported by the folder's
name: `@/pages/checkout`, `@/hooks/order`. A file that only supports that one folder sits **beside**
the index instead of getting a folder of its own — `context/auth/useAuth.ts`,
`services/order/types.ts`.

Folder names are kebab-case, matching `components/ui/` as shadcn generates it. What they export is
PascalCase for components, camelCase for everything else.

| Folder | Holds | Notes |
| :-- | :-- | :-- |
| `pages/` | one folder per page | Pages compose components; they hold almost no markup of their own |
| `components/` | everything split out of a page | Grouped by the thing it is about: `product/`, `cart/`, `order/`, `checkout/`, `address/`, `auth/`, `layout/`, `shared/`, plus `ui/` from shadcn |
| `layouts/` | the frames pages sit in | `main-layout` is the top bar, the page and the toaster |
| `routes/` | which address shows which page | Nothing else decides that |
| `services/` | one class per entity, calling the API through axios | `Product.list()`, `Cart.addItem()`. **Static methods**: there is nothing per-instance to hold |
| `hooks/` | the TanStack Query hooks | `useProducts`, `useCart`, `usePlaceOrder`. A page never calls a service directly |
| `context/` | React context | `context/auth` is the session: provider in `index.tsx`, `useAuth` beside it |
| `config/` | how a library is set up | `config/axios` (instance, interceptors, `ApiError`), `config/query-client` |
| `utils/` | helper functions, grouped like `constants/` | `utils/shared` (`cn`, `money`), `utils/order`, `utils/cart`, `utils/address` |
| `constants/` | constants, grouped by subject | `constants/shared`, `constants/order`, `constants/query-keys` |

Two rules that come out of the structure:

- **A service class and its model types cannot share a name**, so the types live in
  `services/<entity>/types.ts` and are imported from there: `import { Product } from '@/services/product'`
  is the class, `import type { Product } from '@/services/product/types'` is the shape of a product.
- **`ui/` is generated.** `npx shadcn@latest add <component>` writes it, oxlint ignores it, and it is
  not edited by hand. Its components import `{ cn } from "cn"`, which is an alias mapped in
  `vite.config.ts` and `tsconfig.app.json` to `utils/shared/cn.ts`, so a freshly added component needs
  no editing.

## How it is put together

- `config/axios` — the only way to call the backend. One instance with `baseURL: '/api'`, an
  interceptor that attaches the access token, and one that turns every failure into an `ApiError`
  (status plus per-field messages) and retries once after a silent refresh on a 401.
- **The access token lives in memory only.** The refresh token is Identity's HttpOnly cookie, which
  JavaScript never sees; the Vite proxy keeps the browser on one origin so that cookie just works.
- **Server state belongs to TanStack Query.** A mutation invalidates what it affected rather than
  editing a local copy, because prices, availability and totals are the server's to decide. Signing
  out clears the cache: whose cart is held has to change with who is signed in.
