import type { Order, Shipment } from '@/services/order/types'

/** A parcel the customer can say arrived (specs/040): shipped, and not received yet. */
export const canReceive = (shipment: Shipment): boolean =>
  shipment.status === 'Shipped' && !shipment.deliveredAt && Boolean(shipment.id)

/** Every parcel of the order has been received - what the order then reads as. */
export function allDelivered(order: Pick<Order, 'status' | 'shipments'>): boolean {
  const shipments = order.shipments ?? []
  return order.status === 'Shipped' && shipments.length > 0 && shipments.every((s) => Boolean(s.deliveredAt))
}
