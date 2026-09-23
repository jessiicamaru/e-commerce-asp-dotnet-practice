import { AxiosError } from 'axios'

/** A server refusal as axios delivers it: a status and the ProblemDetails body it carries. */
export function refusal(status: number, detail: string) {
  const error = new AxiosError('failed')
  error.response = {
    status,
    data: { status, detail },
    statusText: '',
    headers: {},
    config: { headers: {} as never },
  }
  return error
}
