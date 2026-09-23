// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@/config/axios'
import type {
  CheckoutChoice,
  Order as OrderModel,
  OrderPage,
  Quote,
  Sale,
  SalePage,
  ShippingOption,
} from './types'

/**
 * Checkout and orders. The customer chooses only where and how; what is bought comes from their cart,
 * what it costs from Catalog, Order's delivery options and the destination's tax rate.
 */
export class Order {
  /** Public: delivery prices are decided by Order, never by the client. */
  static async shippingOptions(): Promise<ShippingOption[]> {
    const { data } = await http.get<ShippingOption[]>('/orders/shipping-options', { anonymous: true })
    return data
  }

  /** What checkout would charge, in the same parts, without placing anything (#38). */
  static async quote({ addressId, shippingOption }: CheckoutChoice): Promise<Quote> {
    const params = new URLSearchParams({ shippingOption })
    if (addressId) {
      params.set('addressId', addressId)
    }

    const { data } = await http.get<Quote>(`/orders/quote?${params}`)
    return data
  }

  static async place(choice: CheckoutChoice): Promise<OrderModel> {
    const { data } = await http.post<OrderModel>('/orders', choice)
    return data
  }

  static async get(id: string): Promise<OrderModel> {
    const { data } = await http.get<OrderModel>(`/orders/${id}`)
    return data
  }

  /** The caller's own orders: there is no user id in the request (Constitution IV). */
  static async listMine(page: number, pageSize: number): Promise<OrderPage> {
    const { data } = await http.get<OrderPage>(`/orders?page=${page}&pageSize=${pageSize}`)
    return data
  }

  /**
   * The signed-in seller's sales (specs/034). Like `listMine`, the request names nobody: Order reads
   * the seller from the token, so there is no id here to change into somebody else's.
   */
  static async sales(page: number, pageSize: number): Promise<SalePage> {
    const { data } = await http.get<SalePage>(`/orders/sales?page=${page}&pageSize=${pageSize}`)
    return data
  }

  /** One sale, the seller's own lines only. Not theirs and not there are the same 404. */
  static async sale(id: string): Promise<Sale> {
    const { data } = await http.get<Sale>(`/orders/sales/${id}`)
    return data
  }

  /** The seller starts preparing THEIR part of this order (specs/035). The token says whose. */
  static async prepareSale(id: string): Promise<Sale> {
    const { data } = await http.post<Sale>(`/orders/sales/${id}/preparing`)
    return data
  }

  /** The seller has sent THEIR part, with the carrier's tracking reference. */
  static async shipSale(id: string, trackingReference: string): Promise<Sale> {
    const { data } = await http.post<Sale>(`/orders/sales/${id}/shipment`, { trackingReference })
    return data
  }
}
