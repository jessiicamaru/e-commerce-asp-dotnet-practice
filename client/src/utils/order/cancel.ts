import { ORDER_STATUS } from '@/constants/order'
import type { Order } from '@/services/order/types'

/**
 * Whether to OFFER cancelling (specs/039) - the server decides, and refuses on its own. Drawn the same
 * way the server decides, so a button is not offered only to be refused.
 */

/** A customer: a paid order whose every parcel is still waiting - a part cancelled on its own (specs/104) is done. */
export function customerCanCancel(order: Pick<Order, 'status' | 'shipments'>): boolean {
  const parts = order.shipments ?? []
  // An order an older image wrote has no parts at all: the order's own status decides.
  return (
    order.status === ORDER_STATUS.paid &&
    parts.every((s) => s.status === 'Paid' || s.status === 'Cancelled') &&
    (parts.length === 0 || parts.some((s) => s.status === 'Paid'))
  )
}

/** Staff: a paid order, even being prepared, until its first parcel has shipped. */
export function staffCanCancel(order: Pick<Order, 'status' | 'shipments'>): boolean {
  return (
    (order.status === ORDER_STATUS.paid || order.status === ORDER_STATUS.preparing) &&
    !(order.shipments ?? []).some((s) => s.status === 'Shipped')
  )
}
