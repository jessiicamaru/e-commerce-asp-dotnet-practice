/**
 * Where the other app is (specs/137, ADR-003): the storefront links staff to the back office, and the back office links
 * to products on the storefront. Read at run time, because one image runs anywhere (specs/051): in a container nginx
 * writes `window.__APP_CONFIG__` into `/app-config.js` from `STOREFRONT_URL` and `BACK_OFFICE_URL`; in development that
 * file is each app's `public/app-config.js`, which says nothing, and the defaults below apply.
 */
export interface AppConfig {
  storefrontUrl?: string
  backOfficeUrl?: string
}

declare global {
  interface Window {
    __APP_CONFIG__?: AppConfig
  }
}

/** The development addresses: two HOSTS, not two ports, because cookies ignore the port (specs/136). */
export const DEVELOPMENT_ADDRESSES = {
  storefrontUrl: 'http://localhost:5173',
  backOfficeUrl: 'http://portal.localhost:5174',
} as const

export type AppName = 'storefront' | 'back-office'

let thisApp: AppName = 'storefront'

/** Which app is running - each app's main.tsx says so once. Unset means the storefront, which tests rely on. */
export function setCurrentApp(app: AppName) {
  thisApp = app
}

export function currentApp(): AppName {
  return thisApp
}

function base(key: keyof AppConfig): string {
  const configured = typeof window === 'undefined' ? undefined : window.__APP_CONFIG__?.[key]
  return (configured?.trim() || DEVELOPMENT_ADDRESSES[key]).replace(/\/+$/, '')
}

/** An address on the storefront, absolute: `storefrontUrl('/products/1')`. */
export function storefrontUrl(path = '/'): string {
  return base('storefrontUrl') + (path.startsWith('/') ? path : `/${path}`)
}

/** An address in the back office, absolute: `backOfficeUrl('/users')`. */
export function backOfficeUrl(path = '/'): string {
  return base('backOfficeUrl') + (path.startsWith('/') ? path : `/${path}`)
}

/**
 * Where a storefront address of the console from before the move (`/admin/...`, specs/137) now lives: the same page in
 * the back office, query and all. Old bookmarks, emails and notices stored with such links all arrive through this.
 */
export function consoleAddress(path: string): string | null {
  const match = /^\/admin(?=$|[/?#])(.*)$/.exec(path)
  if (!match) return null
  const rest = match[1]
  return backOfficeUrl(rest === '' || rest.startsWith('?') || rest.startsWith('#') ? `/${rest}` : rest)
}
