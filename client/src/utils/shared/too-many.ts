import type { TFunction } from 'i18next'
import { ApiError } from '@/config/axios'

/**
 * "Too many attempts. Try again in N minutes." for a 429 (specs/062), or null for anything else.
 *
 * The gateway (per client) and Identity (per email) both send how long to wait, as `retryAfter` and as
 * `Retry-After`, in seconds. A person reads minutes, so it is rounded up and never zero.
 */
export function tooManyAttempts(t: TFunction, caught: unknown): string | null {
  const error = ApiError.from(caught)
  if (error.status !== 429) return null

  const seconds = error.retryAfterSeconds
  if (seconds === null) return t('common:error.tooManyAttemptsLater')

  return t('common:error.tooManyAttempts', { count: Math.max(1, Math.ceil(seconds / 60)) })
}
