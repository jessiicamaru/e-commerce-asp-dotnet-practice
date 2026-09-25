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
  cancelled: 'Cancelled',
} as const

/** Still waiting on the saga: stock is being reserved and payment taken. */
export const isSettling = (status: string) => status === ORDER_STATUS.submitted

/**
 * Days after delivery a parcel may be returned (specs/066, `Returns:WindowDays` on the server) - and days to
 * take a refusal further or to send an accepted parcel back. For DRAWING only: the server decides, and a page
 * that disagrees with it is refused with a 409 naming the window (specs/067 research D1).
 */
export const RETURN_WINDOW_DAYS = 7
