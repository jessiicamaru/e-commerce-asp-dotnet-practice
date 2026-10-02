# Research: The admin and moderator console moves to the back office

## D1 - Shared components go to `packages/core/src/components`

**Decision**: The 36 components used by both the console and the shop move to `packages/core/src/components`, keeping
their `<area>/<thing>` folders.

**Rationale**: An import-graph analysis of the storefront found:
- no component used only by the console;
- 36 used by both, which depend only on each other, the core and the kit.

They draw with the kit and use the core, so they cannot live in `packages/ui`, which never imports the core
(specs/135). specs/136 D2 set the rule this follows: components that need the core live in the core.

**Alternatives rejected**:
- Copying them: two order rows, two parcel-action panels, drifting apart.
- A new `packages/components` package: a third package for the same dependency set. Reconsider if the core grows past
  what one package can explain.

## D2 - Pages keep their names; addresses lose `/admin`

**Decision**: The page folders keep their names (`pages/admin-users`), so the move is a rename git can follow. Their
addresses drop the prefix: `/admin/users` becomes `/users`.

**Rationale**: The whole back office is the console, so `/admin` adds nothing to an address. Keeping folder names keeps
`git log --follow`, the specs that cite them, and the tests untouched apart from their paths.

## D3 - Each app learns the other's address at run time

**Decision**: `core/config/apps` reads `window.__APP_CONFIG__`. In a container, nginx writes it into `/app-config.js`
from `STOREFRONT_URL` and `BACK_OFFICE_URL`, the way it already renders `GATEWAY_URL`. In development the same file is
served from each app's `public/` and holds nothing, so the defaults apply: `http://localhost:5173` and
`http://portal.localhost:5174`.

**Rationale**: An image is built once and runs anywhere (specs/051), so an address cannot be baked in at build time.

**Alternatives rejected**:
- Vite `import.meta.env` at build time: one image per environment.
- Deriving one address from the other (`portal.` + host): breaks as soon as the ports differ, which they do in
  development and in compose.

## D4 - Old addresses and old notices still arrive

**Decision**: The storefront keeps a route `/admin/*` that sends the browser to the same path in the back office. Notice
links starting with `/admin` are mapped the same way when opened.

**Rationale**: Identity stores notices with `/admin/email-delivery` and `/admin` links, and people keep bookmarks.
Changing the server's link would leave the stored notices broken, while mapping at the reader covers both old and new.

## D5 - Closing a shop moves to the back office

**Decision**: `CloseShop` leaves the shop's public page. It is offered:
- on an approved shop application (Shops, Approved), which has the seller and the shop's name;
- on a seller's row in People.

**Rationale**: After #278 the storefront never holds a staff session, so a staff action there would be a 403. The two
places cover every shop: an application for shops opened since specs/044, and the seller's own row for those from before.

## D6 - Links to the storefront open in a new tab

**Decision**: A console link to a product page is an absolute link to the storefront, opened in a new tab.

**Rationale**: It is another application. Opening it in place would leave the console, and its session, behind.
