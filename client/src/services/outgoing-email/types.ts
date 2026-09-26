/** What became of one email (specs/087) - never its data, which for a reset link is a token. */
export interface OutgoingEmail {
  id: string
  recipientId: string
  recipientEmail: string | null
  template: string
  language: string
  status: OutgoingEmailStatus
  attempts: number
  lastError: string | null
  createdAt: string
  nextAttemptAt: string
  sentAt: string | null
  /** A failed email an administrator may send again - never a reset or confirmation link. */
  canRetry: boolean
}

export const OUTGOING_EMAIL_STATES = ['Failed', 'Pending', 'Sent'] as const
export type OutgoingEmailStatus = (typeof OUTGOING_EMAIL_STATES)[number]

export interface OutgoingEmailPage {
  items: OutgoingEmail[]
  page: number
  pageSize: number
  totalCount: number
}
