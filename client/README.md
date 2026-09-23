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
| `layouts/` | the frames pages sit in | `main-layout` is the top bar, the page and the toaster; `seller-layout` is the sidebar every `/shop` page sits inside |
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
- **`ui/` is generated.** `npx shadcn@latest add <component>` writes it, oxlint ignores it, and its
  components import `{ cn } from "cn"`, an alias mapped in `vite.config.ts` and `tsconfig.app.json` to
  `utils/shared/cn.ts`, so a freshly added component needs no editing.
  ⚠️ **Four files in it ARE edited, on purpose, and `shadcn add --overwrite` would undo it:**
  `select.tsx`, `combobox.tsx`, `dropdown-menu.tsx` and `alert-dialog.tsx`. Their items were `py-1 pl-1.5` inside a popup
  this theme rounds to 1rem, so the text sat against the edge; they are now `py-2 pl-3`, with the
  popups padded to match. And `SelectContent` defaults `alignItemWithTrigger` to **false**: base-nova's
  `true` places the chosen item exactly over the trigger, macOS-style, which read as the dropdown
  covering its own button. Fixing it in the component, once, is what shadcn intends - fixing it at
  every call site is how one of them gets missed.
  `AlertDialogAction` was generated as a plain `Button` that **does not close the dialog**; it is now
  a `Close` like `AlertDialogCancel`, because a confirmed dialog left open hid the refusal the page
  showed next. `address-card`'s tests fail if it regresses.
- **Use shadcn before writing a component.** Dialogs, menus, comboboxes, tabs, sheets, pagination,
  tooltips all come from `ui/`. The few things here that are not - `shared/image-dropzone`,
  `shared/quantity-stepper` - exist because shadcn has no such component, and each is a thin
  arrangement of `ui/` pieces. `shared/searchable-select` and `shared/pager` are not new components
  either: they assemble shadcn's Combobox and Pagination once, so the three dropdowns that need search
  and the four paged lists behave alike.
- **A dialog is for a short task you come back from** - adding an address at checkout, renaming the
  shop, confirming a withdrawal. Not for anything with its own address or more than one form's worth
  of fields; those stay pages. `window.confirm` is not used: an `AlertDialog` says what will happen.
- **One page size, `PAGE_SIZE` (12) in `constants/shared`,** for every paged list, and the server's
  queries default to the same number. 12 divides into a grid of 4, 3 or 2.

## How it is put together

- `config/axios` — the only way to call the backend. One instance with `baseURL: '/api'`, an
  interceptor that attaches the access token, and one that turns every failure into an `ApiError`
  (status plus per-field messages) and retries once after a silent refresh on a 401.
- **The access token lives in memory only.** The refresh token is Identity's HttpOnly cookie, which
  JavaScript never sees; the Vite proxy keeps the browser on one origin so that cookie just works.
- **Server state belongs to TanStack Query.** A mutation invalidates what it affected rather than
  editing a local copy, because prices, availability and totals are the server's to decide. Signing
  out clears the cache: whose cart is held has to change with who is signed in.

## Tests

```bash
npm test          # once
npm run test:watch
```

Vitest, jsdom, Testing Library. They arrived with specs/028; before that this folder had **no tests
at all**, and CI only linted and built it — so the storefront could render nothing and still go
green.

The rules, each of which exists because of a way a front-end suite rots:

- **Nothing reaches the network.** `src/test/setup.ts` replaces `fetch` with one that throws, so a
  test that forgot to stub a service call fails loudly instead of passing slowly against whatever
  happens to be running on the machine. Stub the **service class** (`vi.spyOn(Product, 'mine')`),
  not axios: the service is the seam, and a test written against axios asserts the shape of a
  library rather than the shape of a request.
- **Shared render helpers live in `src/test/`**, never in a `.test.tsx`: importing from a test file
  runs that file's suites again inside the importing one. `renderAsSeller` is there.
- **Pin the language.** i18next's detector reads `navigator.language`, so a suite that does not set
  one asserts English on one machine and Vietnamese on another. `await i18n.changeLanguage('en')` in
  `beforeEach`, and test the Vietnamese page deliberately rather than by accident.
- **Test what can be wrong.** What a hook asks for, what a guard lets through, what a form sends,
  how a server refusal reaches a person. Not that a `div` rendered. The tests worth having here are
  the ones naming a defect: *the price field must say VND while the shop is being read in USD*.

A layout fault is not unit-testable and should not be faked — two of this feature's defects (an
image drawn over the price editor, a price box empty when a price existed) were found in a
screenshot, and that is the honest tool for them.
