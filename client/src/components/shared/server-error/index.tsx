import { ApiError } from '@/config/axios'

/**
 * What the server said, in words, to the person who caused it (specs/028).
 *
 * <p>
 * The services go out of their way to make this possible: `GlobalExceptionHandler` sends the
 * `detail` of a validation, conflict or not-found refusal even outside Development, precisely so a
 * caller can repeat it to somebody. "9.99 is not an amount VND can hold" and "A product with SKU
 * 'X100' already exists" are answers a seller can act on; "Something went wrong" is not.
 * </p>
 * <p>
 * A 500 is the exception: its detail is deliberately hidden by the server, so there is nothing to
 * show and the fallback is used.
 * </p>
 */
export function ServerError({ error, fallback }: { error: unknown; fallback: string }) {
  if (!error) {
    return null
  }

  const problem = ApiError.from(error)
  const detail = problem.status >= 500 ? undefined : problem.problem.detail

  return (
    <p role="alert" className="text-destructive text-sm">
      {detail ?? fallback}
    </p>
  )
}
