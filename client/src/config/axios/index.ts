import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { currentLanguage } from '@/config/i18n'
import { CURRENCY_HEADER, currentCurrency } from '@/config/money'
import { ApiError } from './api-error'

export * from './api-error'

/**
 * The one way the storefront talks to the backend: through the gateway, under `/api` (feature 014).
 *
 * In development Vite proxies `/api` to `:5000`, so the browser sees a single origin - no CORS, and
 * Identity's refresh cookie is a same-origin HttpOnly cookie exactly as it would be behind a real
 * reverse proxy. `withCredentials` is what sends it.
 */
export const http = axios.create({
  baseURL: '/api',
  withCredentials: true,
  headers: { Accept: 'application/json' },
})

/**
 * `anonymous: true` on a request means: do not attach the access token, and do not try a refresh if it
 * comes back 401. Declared on axios' own config so every call site is type-checked (sign-in, sign-up,
 * refresh, logout and everything a shopper may do without an account).
 */
declare module 'axios' {
  interface AxiosRequestConfig {
    anonymous?: boolean
    /** Set by the interceptor, so one request is retried at most once. */
    retried?: boolean
  }
}

type Config = InternalAxiosRequestConfig

let tokenProvider: () => string | null = () => null
let onUnauthorized: () => Promise<boolean> = async () => false

/**
 * Lets the auth layer supply the in-memory access token and the single shared refresh.
 * Called once by `AuthProvider`; nothing else should call it.
 */
export function configureAuth(provider: () => string | null, refresh: () => Promise<boolean>) {
  tokenProvider = provider
  onUnauthorized = refresh
}

http.interceptors.request.use((config) => {
  const request = config as Config

  // The interface and the product text have to agree, so every call says which language this is
  // (specs/021). The server negotiates it the standard way and answers with Content-Language.
  request.headers['Accept-Language'] = currentLanguage()

  // ...and which currency it wants its amounts in (specs/022). A different header, because it is a
  // different choice: a Vietnamese person reading English still pays in dong.
  //
  // `??=`, not `=`: a caller that set this header meant it. The seller's own listing page asks for
  // the SAME product once per currency, because a response carries one currency's prices and a
  // seller editing prices has to see the ones they are not currently browsing in. Overwriting here
  // made both requests identical and left an existing VND price showing as an empty box.
  request.headers[CURRENCY_HEADER] ??= currentCurrency()
  const token = request.anonymous ? null : tokenProvider()
  if (token) {
    request.headers.Authorization = `Bearer ${token}`
  }
  return request
})

http.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    const request = error instanceof AxiosError ? (error.config as Config | undefined) : undefined

    // An expired access token is normal after 15 minutes: refresh once, then retry the request once.
    if (error instanceof AxiosError && error.response?.status === 401 && request && !request.anonymous && !request.retried) {
      request.retried = true
      if (await onUnauthorized()) {
        return http.request(request)
      }
    }

    // Every caller above this line sees an ApiError, never an AxiosError.
    throw ApiError.from(error)
  },
)
