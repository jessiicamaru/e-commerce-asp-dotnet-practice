import type { TFunction } from 'i18next'
import { ApiError } from '@ecommerce/core/config/axios'

/** The reasons Order gives for keeping an account open (specs/112). */
export const DELETION_BLOCKERS = ['OpenOrders', 'OpenReturns', 'OpenSales', 'UnpaidEarnings'] as const
type Blocker = (typeof DELETION_BLOCKERS)[number]

const isBlocker = (reason: string): reason is Blocker => (DELETION_BLOCKERS as readonly string[]).includes(reason)

/**
 * Why a deletion was refused, in the reader's language (specs/112): each open reason on its own line, or the staff
 * rule. A reason this page cannot word - a newer Order's - brings the server's own sentence along, so nothing is
 * hidden. Null when it was not a 409 (a wrong password is shown on its field instead).
 */
export function deletionRefusal(error: unknown, t: TFunction<'auth'>): string[] | null {
  const failure = ApiError.from(error)
  if (failure.status !== 409) return null

  const problem = failure.problem
  const sentence = problem.detail ? [problem.detail] : []
  if (problem.code === 'StaffAccount') return [t('account.deleteStaff')]
  if (problem.code !== 'AccountHasOpenBusiness') return sentence

  const reasons = problem.reasons ?? []
  const worded = reasons.filter(isBlocker).map((reason) => t(`account.deleteBlockers.${reason}`))
  return worded.length > 0 && worded.length === reasons.length ? worded : [...worded, ...sentence]
}
