import { AxiosError } from 'axios'

/** RFC 7807 ProblemDetails - what every service answers with (the shared GlobalExceptionHandler). */
export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
  /** A 403's facts, beside its sentence (specs/049) - e.g. `AccountLocked` at sign-in. */
  code?: string
  /** ISO 8601, UTC. */
  until?: string
  reason?: string
}

/**
 * One error type for every failed call, so a page never unpacks an AxiosError itself.
 */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Request failed with ${status}`)
    this.status = status
    this.problem = problem
  }

  /** Field-level validation messages, flattened: `{ PostalCode: "..." }`. Server names are PascalCase. */
  get fieldErrors(): Record<string, string> {
    const out: Record<string, string> = {}
    for (const [field, messages] of Object.entries(this.problem.errors ?? {})) {
      out[field] = messages[0] ?? ''
    }
    return out
  }

  /** The same messages keyed as a form names its fields (camelCase). */
  get formErrors(): Record<string, string> {
    return Object.fromEntries(
      Object.entries(this.fieldErrors).map(([field, message]) => [field.charAt(0).toLowerCase() + field.slice(1), message]),
    )
  }

  static from(error: unknown): ApiError {
    if (error instanceof ApiError) {
      return error
    }

    if (error instanceof AxiosError) {
      const status = error.response?.status ?? 0
      const data = error.response?.data
      const problem: ProblemDetails = data && typeof data === 'object' ? (data as ProblemDetails) : {}
      return new ApiError(status, { ...problem, status })
    }

    // No response at all: the gateway is down, or the browser refused the request.
    return new ApiError(0, { title: 'The server could not be reached.' })
  }
}
