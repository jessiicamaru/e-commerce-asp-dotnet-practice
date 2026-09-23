import type { Order } from '@/services/order/types'

/**
 * The shop's own parcel of an order (specs/038 research D1) - what staff prepare and send - or `null`
 * when the order holds none of the shop's goods and every parcel is a seller's to send.
 *
 * An order an older image wrote has no parcels at all (specs/035 D3); then the whole order is the shop's,
 * in the order's own state, which is exactly what the server's staff endpoints move.
 */
export function shopParcelOf(order: Order): { status: string; trackingReference: string | null; items: string[] } | null {
  const shipments = order.shipments ?? []

  if (shipments.length === 0) {
    return {
      status: order.status,
      trackingReference: order.trackingReference,
      items: order.items.map((i) => (i.optionSummary ? `${i.productName} · ${i.optionSummary}` : i.productName)),
    }
  }

  const shop = shipments.find((s) => s.isShop)
  return shop ? { status: shop.status, trackingReference: shop.trackingReference, items: shop.items } : null
}
