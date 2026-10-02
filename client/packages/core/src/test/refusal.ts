import { AxiosError } from 'axios'

/**
 * A server refusal as axios delivers it: a status and the ProblemDetails body it carries, with any
 * extensions the handler added (specs/049: `code`, `until`, `reason` on a sign-in refusal).
 */
export function refusal(status: number, detail?: string, extensions: Record<string, unknown> = {}) {
  const error = new AxiosError('failed')
  error.response = {
    status,
    data: { status, detail, ...extensions },
    statusText: '',
    headers: {},
    config: { headers: {} as never },
  }
  return error
}
