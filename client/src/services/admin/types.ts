/** What is due now to one seller in one currency (specs/037) - staff's worklist. */
export interface PayoutDue {
  sellerId: string
  /** The latest name frozen on their lines (specs/036), or null. */
  sellerName: string | null
  currency: string
  due: number
  /** How many shipped parcels the amount covers. */
  parts: number
}

/** The shop's parcel moves through these; the queue lists one of them at a time. */
export const QUEUE_STATES = ['Paid', 'Preparing', 'Shipped'] as const
export type QueueState = (typeof QUEUE_STATES)[number]
