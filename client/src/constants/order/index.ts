/** How often a just-placed order is asked about while the saga settles it. */
export const ORDER_POLL_MS = 1000

/**
 * How long to keep asking. The saga settles in about two seconds; past this the page says so instead
 * of spinning forever.
 */
export const ORDER_POLL_LIMIT_MS = 30_000

/** Order statuses, as Order reports them (`Completed` is reported as `Paid` - specs/011). */
export const ORDER_STATUS = {
  submitted: 'Submitted',
  paid: 'Paid',
  preparing: 'Preparing',
  shipped: 'Shipped',
  failed: 'Failed',
} as const

/** Still waiting on the saga: stock is being reserved and payment taken. */
export const isSettling = (status: string) => status === ORDER_STATUS.submitted
