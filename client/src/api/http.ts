// The one way the storefront talks to the backend: through the gateway, under /api.
//
// Errors come back as RFC 7807 ProblemDetails from every service (the shared GlobalExceptionHandler),
// so they are turned into one ApiError with the status, a title, and any per-field validation errors -
// the page decides how to word them for a person.

export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Request failed with ${status}`)
    this.status = status
    this.problem = problem
  }

  /** Field-level validation messages, flattened: { PostalCode: "..." }. */
  get fieldErrors(): Record<string, string> {
    const out: Record<string, string> = {}
    for (const [field, messages] of Object.entries(this.problem.errors ?? {})) {
      out[field] = messages[0] ?? ''
    }
    return out
  }
}

/** Supplies the current access token. Set by the auth layer; the token lives in memory only. */
let tokenProvider: () => string | null = () => null
/** Called once on a 401 to try a silent refresh; returns true if a new token was obtained. */
let onUnauthorized: () => Promise<boolean> = async () => false

export function configureAuth(provider: () => string | null, refresh: () => Promise<boolean>) {
  tokenProvider = provider
  onUnauthorized = refresh
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** Skip the Authorization header and the refresh-on-401 retry (sign-in, sign-up, refresh). */
  anonymous?: boolean
}

export async function api<T>(path: string, options: RequestOptions = {}, retried = false): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  const token = options.anonymous ? null : tokenProvider()
  if (token) headers.Authorization = `Bearer ${token}`

  const response = await fetch(`/api${path}`, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    // Same origin through the proxy; 'include' also sends the HttpOnly refresh cookie.
    credentials: 'include',
  })

  // An expired access token is normal after 15 minutes: refresh once, then retry the request.
  if (response.status === 401 && !options.anonymous && !retried && (await onUnauthorized())) {
    return api<T>(path, options, true)
  }

  if (!response.ok) {
    let problem: ProblemDetails = { status: response.status }
    try {
      problem = { status: response.status, ...(await response.json()) }
    } catch {
      /* not JSON - keep the status */
    }
    throw new ApiError(response.status, problem)
  }

  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}
