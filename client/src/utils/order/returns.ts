import { RETURN_WINDOW_DAYS } from '@/constants/order'
import type { ParcelReturn, Shipment } from '@/services/order/types'

/**
 * What to OFFER on a parcel's return (specs/066, 067) - each a copy of the server's guard, so a button is not
 * drawn only to be refused. The server decides on its own either way; `now` is a parameter so the window can
 * be tested without a clock.
 */

const DAY_MS = 24 * 60 * 60 * 1000

/** When the window that opened at `from` closes. */
export function returnDeadline(from: string): Date {
  return new Date(new Date(from).getTime() + RETURN_WINDOW_DAYS * DAY_MS)
}

const open = (from: string | null | undefined, now: Date) => Boolean(from) && now < returnDeadline(from!)

/** The buyer may ask: a delivered parcel, inside the window of its delivery, with no return yet. */
export function canRequestReturn(shipment: Shipment, now: Date = new Date()): boolean {
  return shipment.status === 'Shipped' && Boolean(shipment.id) && !shipment.return && open(shipment.deliveredAt, now)
}

/** The buyer may take a refusal to staff, within the window of that refusal. */
export function canEscalate(ret: ParcelReturn, now: Date = new Date()): boolean {
  return ret.status === 'Refused' && open(ret.decidedAt, now)
}

/** The buyer may say they sent an accepted parcel back, within the window of the acceptance. */
export function canSendBack(ret: ParcelReturn, now: Date = new Date()): boolean {
  return ret.status === 'Accepted' && open(ret.decidedAt, now)
}

/** The one thing a seller or staff can do next: answer the request, or say the parcel came back. */
export type ReturnStep = 'decide' | 'receive' | null

/** A seller answers a request and receives what was sent back - never an escalated one, which is staff's. */
export function sellerReturnStep(ret: ParcelReturn): ReturnStep {
  if (ret.status === 'Requested') return 'decide'
  if (ret.status === 'SentBack') return 'receive'
  return null
}

/**
 * Staff give the final word on ANY escalated return, and otherwise act for the shop's own parcel as a seller
 * does for theirs. A seller's parcel that is not escalated is not staff's to move.
 */
export function staffReturnStep(ret: ParcelReturn): ReturnStep {
  if (ret.status === 'Escalated') return 'decide'
  return ret.isShop ? sellerReturnStep(ret) : null
}

/** A decision on an escalated return is the last one: a refusal there is a rejection for good. */
export const isFinalDecision = (ret: ParcelReturn) => ret.status === 'Escalated'
