import { ORDER_STATUS } from '@/constants/order'
import type { Order } from '@/services/order/types'

/**
 * Whether to OFFER cancelling (specs/039) - the server decides, and refuses on its own. Drawn the same
 * way the server decides, so a button is not offered only to be refused.
 */

/** A customer: a paid order whose every parcel is still waiting. */
export function customerCanCancel(order: Pick<Order, 'status' | 'shipments'>): boolean {
  return order.status === ORDER_STATUS.paid && (order.shipments ?? []).every((s) => s.status === 'Paid')
}

/** Staff: a paid order, even being prepared, until its first parcel has shipped. */
export function staffCanCancel(order: Pick<Order, 'status' | 'shipments'>): boolean {
  return (
    (order.status === ORDER_STATUS.paid || order.status === ORDER_STATUS.preparing) &&
    !(order.shipments ?? []).some((s) => s.status === 'Shipped')
  )
}
