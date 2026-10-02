# Research: The client becomes a workspace of apps and packages

## D1 - npm workspaces, not a monorepo tool

**Decision**: Use plain npm workspaces, with one lock file at `client/`.

**Rationale**: npm is already the client's package manager. Two apps and two packages do not need task graphs or
remote caching.

**Alternatives rejected**:
- Nx or Turborepo: another tool to learn and configure, for a build that takes seconds.
- pnpm: changes the CI cache and every command for no gain at this size.

## D2 - Packages are source, resolved by alias

**Decision**: `@ecommerce/core/*` and `@ecommerce/ui/*` are aliases into each package's `src/`, set in
`tsconfig.base.json` and in a shared Vite alias helper. A package has no build of its own; the app that imports it
bundles it.

**Rationale**: This is exactly how `@/` works today, so the toolchain does not change. Directory imports
(`services/order` → `index.ts`) keep working, which `package.json` `exports` patterns would not do.

**Alternatives rejected**:
- Building each package to `dist/`: a watch process per package in development, for nothing.
- `exports` maps: they cannot express "a folder means its `index.ts`".

## D3 - `@/` keeps meaning "this app"

**Decision**: Inside an app, `@/` is that app's `src/`. Shared code is always imported by package name. A package
never uses `@/`.

**Rationale**: If `@/` meant different folders in different places, an import in a package would resolve into
whichever app happened to compile it.

**Alternatives rejected**: Leaving `@/` imports in packages. They compile in one app and break in the other.

## D4 - What is shared

**Decision**:
- `packages/core`: `config`, `context`, `services`, `hooks`, `utils`, `constants`, `locales`, `test`.
- `packages/ui`: the shadcn kit and `cn`.
- The app keeps `components`, `pages`, `layouts` and `routes`.

**Rationale**: The import graph shows these folders never import app code, except three tests, which move to the app.
Components shared by the two apps' pages move to a package when #277 needs them, not before.

**Alternatives rejected**: Moving components now. That would guess which ones the back office uses.

## D5 - Tailwind must scan the packages

**Decision**: The app's stylesheet adds `@source` for `packages/ui/src` and `packages/core/src`.

**Rationale**: Tailwind v4 finds class names by scanning the app's own tree. Source outside it is not scanned, so the
kit's classes would silently disappear: the build passes and the page is unstyled.

## D6 - `cn` belongs to the kit

**Decision**: `cn` lives in `packages/ui/src/cn.ts`, and the `cn` alias points there. App code imports it from `cn`,
as the generated components already do.

**Rationale**: It is the kit's helper, and the kit must not depend on core.
