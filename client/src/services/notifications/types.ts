/** One notification (specs/042): a kind and its data - the storefront words it in the reader's language. */
export interface AppNotification {
  id: string
  kind: string
  data: Record<string, string>
  /** Where it points, e.g. `/orders/{id}`. */
  link: string | null
  createdAt: string
  /** Null until read. */
  readAt: string | null
}

export interface NotificationPage {
  items: AppNotification[]
  page: number
  pageSize: number
  totalCount: number
}
