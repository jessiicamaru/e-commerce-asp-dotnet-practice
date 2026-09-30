/** A person as staff see them (specs/043). */
export interface Account {
  id: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
  createdAt: string
  /** Null when not locked, or when the lock has run out. */
  lockedUntil: string | null
  lockReason: string | null
  bannedAt: string | null
  banReason: string | null
  /** Whether there is a second factor to reset (specs/110). */
  twoFactorEnabled?: boolean
}

export interface AccountPage {
  items: Account[]
  page: number
  pageSize: number
  totalCount: number
}

/**
 * One decision staff made about a person (specs/100) - from the audit log's Moderation entries about them. A reason
 * when one was given; never the snapshots.
 */
export interface ModerationHistoryEntry {
  id: string
  action: string
  actorEmail: string | null
  actorRole: string | null
  subjectType: string
  subjectId: string | null
  summary: string
  reason: string | null
  occurredAt: string
}

export interface ModerationHistoryPage {
  items: ModerationHistoryEntry[]
  page: number
  pageSize: number
  totalCount: number
}

/** The longest lock a moderator may set; an administrator may go to a year (specs/043). */
export const MODERATOR_MAX_LOCK_DAYS = 30
