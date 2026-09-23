// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { Order, OrderPage, Payout } from '@/services/order/types'
import type { PayoutDue, QueueState } from './types'

/**
 * What staff do after a sale (specs/038): work the shop's own parcels and settle what sellers are owed.
 * Every call is refused by the server for anybody who is not an administrator - the console drawing
 * itself only for them is courtesy, not the permission.
 */
export class Admin {
  /** Every customer's orders whose SHOP parcel is in this state, oldest first - a queue. */
  static async fulfilment(status: QueueState, page: number, pageSize: number): Promise<OrderPage> {
    const { data } = await http.get<OrderPage>(`/orders/fulfilment?status=${status}&page=${page}&pageSize=${pageSize}`)
    return data
  }

  /** Any order's detail - the one read of an order not scoped to its owner. */
  static async order(id: string): Promise<Order> {
    const { data } = await http.get<Order>(`/orders/fulfilment/${id}`)
    return data
  }

  /** The shop's parcel: waiting → being prepared. */
  static async prepare(id: string): Promise<Order> {
    const { data } = await http.post<Order>(`/orders/${id}/preparing`)
    return data
  }

  /** The shop's parcel: being prepared → shipped, with the carrier's reference. */
  static async ship(id: string, trackingReference: string): Promise<Order> {
    const { data } = await http.post<Order>(`/orders/${id}/shipment`, { trackingReference })
    return data
  }

  /** Staff cancel any paid order until its first parcel has shipped (specs/039). */
  static async cancel(id: string): Promise<Order> {
    const { data } = await http.post<Order>(`/orders/fulfilment/${id}/cancel`)
    return data
  }

  static async due(): Promise<PayoutDue[]> {
    const { data } = await http.get<PayoutDue[]>('/orders/payouts/due')
    return data
  }

  /**
   * Settle everything due to one seller in one currency. There is no amount to send: the server pays
   * what is due at that moment and says how much that was.
   */
  static async pay(sellerId: string, currency: string): Promise<Payout> {
    const { data } = await http.post<Payout>('/orders/payouts', { sellerId, currency })
    return data
  }
}
