import type { TFunction } from 'i18next'
import { ApiError } from '@/config/axios'
import { tooManyAttempts } from '@/utils/shared'

/**
 * What the sign-in page says when signing in fails (specs/049).
 *
 * - 401 is one sentence for "no such email" and "wrong password" alike (#28): the page must not reveal
 *   which emails have accounts.
 * - 403 comes only after the right password (specs/043), so its reader is the account's owner and is
 *   owed the reason. Identity sends the facts - `code`, `until`, `reason` - and they are worded here, in
 *   the reader's language and time zone, the way a notification is worded from its data (specs/042).
 *   A 403 this page has no words for shows the server's own sentence.
 * - 429 (specs/062) - too many attempts from this client or for this email - says how long to wait.
 */
export function describeSignInFailure(t: TFunction<'auth'>, language: string, caught: unknown): string {
  const error = ApiError.from(caught)
  const problem = error.problem

  if (error.status === 401) return t('signIn.wrong')

  const wait = tooManyAttempts(t as TFunction, caught)
  if (wait) return wait

  if (error.status === 403) {
    if (problem.code === 'AccountLocked' && problem.until && problem.reason) {
      return t('signIn.locked', { until: new Date(problem.until).toLocaleString(language), reason: problem.reason })
    }
    if (problem.code === 'AccountBanned' && problem.reason) {
      return t('signIn.banned', { reason: problem.reason })
    }
    if (problem.detail) return problem.detail
  }

  return t('signIn.failed')
}

/**
 * What the second step says when a code is refused (specs/110). A wrong or already-used code: type another. A challenge
 * that died - five minutes, or five wrong codes - means starting again from the password, which `restart` says.
 */
export function describeCodeFailure(t: TFunction<'auth'>, caught: unknown): { message: string; restart: boolean } {
  const error = ApiError.from(caught)

  const wait = tooManyAttempts(t as TFunction, caught)
  if (wait) return { message: wait, restart: true }

  if (error.status === 400 && 'Challenge' in error.fieldErrors) return { message: t('twoFactorStep.again'), restart: true }
  if (error.status === 400) return { message: t('twoFactorStep.wrong'), restart: false }

  return { message: t('signIn.failed'), restart: false }
}
