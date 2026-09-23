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
}

export interface AccountPage {
  items: Account[]
  page: number
  pageSize: number
  totalCount: number
}

/** The longest lock a moderator may set; an administrator may go to a year (specs/043). */
export const MODERATOR_MAX_LOCK_DAYS = 30
