/** The audit log's categories (specs/041), in the order the tabs show them. */
export const AUDIT_CATEGORIES = ['Security', 'User', 'Moderation', 'Catalog', 'Order', 'Payment', 'System'] as const
export type AuditCategory = (typeof AUDIT_CATEGORIES)[number]

export interface AuditEntrySummary {
  id: string
  category: AuditCategory
  action: string
  /** Null: the system did it - a sweeper, a message, nobody signed in. */
  actorId: string | null
  actorEmail: string | null
  actorRole: string | null
  subjectType: string
  subjectId: string | null
  summary: string
  service: string
  occurredAt: string
  changeCount: number
}

/** One changed field, old → new; either side null when the field appeared or went. */
export interface AuditChange {
  path: string
  before: unknown
  after: unknown
}

export interface AuditEntry extends AuditEntrySummary {
  recordedAt: string
  before: unknown
  after: unknown
  changes: AuditChange[]
}

export interface AuditPage {
  items: AuditEntrySummary[]
  page: number
  pageSize: number
  totalCount: number
}

export interface AuditFilter {
  category?: AuditCategory
  actor?: string
  action?: string
  /** ISO time; entries at or after it. */
  from?: string
}

export interface CategoryCount {
  category: AuditCategory
  count: number
}
