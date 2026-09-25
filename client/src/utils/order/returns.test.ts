import { describe, expect, it } from 'vitest'
import type { ParcelReturn, ReturnStatus, Shipment } from '@/services/order/types'
import {
  canEscalate,
  canRequestReturn,
  canSendBack,
  isFinalDecision,
  returnDeadline,
  sellerReturnStep,
  staffReturnStep,
} from './returns'

const now = new Date('2026-09-25T12:00:00Z')
const daysAgo = (days: number) => new Date(now.getTime() - days * 86_400_000).toISOString()

const delivered = (deliveredAt: string | null, extra: Partial<Shipment> = {}): Shipment => ({
  id: 's-1', status: 'Shipped', trackingReference: 'VN-1', items: ['Camera'], sellerName: null, isShop: true,
  deliveredAt, ...extra,
})

const ret = (status: ReturnStatus, extra: Partial<ParcelReturn> = {}): ParcelReturn => ({
  id: 'r-1', orderId: 'o-1', shipmentId: 's-1', isShop: false, status, reason: 'Broken', decisionReason: null,
  trackingReference: null, requestedAt: daysAgo(2), decidedAt: daysAgo(1), sentBackAt: null, receivedAt: null,
  refundAmount: null, ...extra,
})

describe('the window', () => {
  it('closes seven days after it opened', () => {
    expect(returnDeadline('2026-09-20T10:00:00Z').toISOString()).toBe('2026-09-27T10:00:00.000Z')
  })
})

describe('canRequestReturn', () => {
  it('offers a parcel delivered inside the window', () => {
    expect(canRequestReturn(delivered(daysAgo(0)), now)).toBe(true)
    expect(canRequestReturn(delivered(daysAgo(6.9)), now)).toBe(true)
  })

  /** The server refuses "delivered on or before now - 7 days"; so does this. */
  it('does not offer one past the window, or exactly at its end', () => {
    expect(canRequestReturn(delivered(daysAgo(8)), now)).toBe(false)
    expect(canRequestReturn(delivered(daysAgo(7)), now)).toBe(false)
  })

  it('does not offer one not delivered, not shipped, without an id, or already being returned', () => {
    expect(canRequestReturn(delivered(null), now)).toBe(false)
    expect(canRequestReturn(delivered(daysAgo(1), { status: 'Preparing' }), now)).toBe(false)
    expect(canRequestReturn(delivered(daysAgo(1), { id: undefined }), now)).toBe(false)
    expect(canRequestReturn(delivered(daysAgo(1), { return: ret('Requested') }), now)).toBe(false)
  })
})

describe('the buyer after a decision', () => {
  it('escalates a refusal only within the window of the refusal', () => {
    expect(canEscalate(ret('Refused', { decidedAt: daysAgo(3) }), now)).toBe(true)
    expect(canEscalate(ret('Refused', { decidedAt: daysAgo(8) }), now)).toBe(false)
    expect(canEscalate(ret('Rejected'), now)).toBe(false)
    expect(canEscalate(ret('Accepted'), now)).toBe(false)
  })

  it('sends back an acceptance only within its window', () => {
    expect(canSendBack(ret('Accepted', { decidedAt: daysAgo(3) }), now)).toBe(true)
    expect(canSendBack(ret('Accepted', { decidedAt: daysAgo(8) }), now)).toBe(false)
    expect(canSendBack(ret('Refused'), now)).toBe(false)
  })
})

describe('whose step it is', () => {
  it('a seller answers a request and receives what was sent back, nothing else', () => {
    expect(sellerReturnStep(ret('Requested'))).toBe('decide')
    expect(sellerReturnStep(ret('SentBack'))).toBe('receive')
    for (const status of ['Accepted', 'Refused', 'Escalated', 'Rejected', 'Received'] as const) {
      expect(sellerReturnStep(ret(status))).toBeNull()
    }
  })

  it("staff give the final word on anybody's escalated return", () => {
    expect(staffReturnStep(ret('Escalated'))).toBe('decide')
    expect(staffReturnStep(ret('Escalated', { isShop: true }))).toBe('decide')
    expect(isFinalDecision(ret('Escalated'))).toBe(true)
    expect(isFinalDecision(ret('Requested'))).toBe(false)
  })

  it("staff act for the shop's own parcel, never for a seller's that is not escalated", () => {
    expect(staffReturnStep(ret('Requested', { isShop: true }))).toBe('decide')
    expect(staffReturnStep(ret('SentBack', { isShop: true }))).toBe('receive')
    expect(staffReturnStep(ret('Requested'))).toBeNull()
    expect(staffReturnStep(ret('SentBack'))).toBeNull()
  })
})
