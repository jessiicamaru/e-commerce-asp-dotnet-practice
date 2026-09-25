// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@/config/axios'
import type {
  Balance,
  CheckoutChoice,
  Order as OrderModel,
  OrderPage,
  ParcelReturn,
  PayoutPage,
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

  /**
   * The caller cancels their own paid order (specs/039). The order is named; the owner is the token, and
   * someone else's order is the same 404 as none.
   */
  static async cancel(id: string): Promise<OrderModel> {
    const { data } = await http.post<OrderModel>(`/orders/${id}/cancel`)
    return data
  }

  /** The caller says one parcel of their order arrived (specs/040). */
  static async receive(orderId: string, shipmentId: string): Promise<OrderModel> {
    const { data } = await http.post<OrderModel>(`/orders/${orderId}/shipments/${shipmentId}/received`)
    return data
  }

  /**
   * The caller asks to return a delivered parcel of their order (specs/066), saying why. The server decides
   * whether it is theirs, delivered, and still inside the window.
   */
  static async requestReturn(orderId: string, shipmentId: string, reason: string): Promise<ParcelReturn> {
    const { data } = await http.post<ParcelReturn>(`/orders/${orderId}/shipments/${shipmentId}/return`, { reason })
    return data
  }

  /** The caller asks staff to look again at a refused return. */
  static async escalateReturn(orderId: string, shipmentId: string): Promise<ParcelReturn> {
    const { data } = await http.post<ParcelReturn>(`/orders/${orderId}/shipments/${shipmentId}/return/escalate`)
    return data
  }

  /** The caller has sent the accepted parcel back, with the carrier's reference. */
  static async sendReturnBack(orderId: string, shipmentId: string, trackingReference: string): Promise<ParcelReturn> {
    const { data } = await http.post<ParcelReturn>(`/orders/${orderId}/shipments/${shipmentId}/return/sent`, {
      trackingReference,
    })
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

  /** The seller accepts the return of THEIR parcel of this sale (specs/066). The token says whose. */
  static async acceptSaleReturn(id: string): Promise<ParcelReturn> {
    const { data } = await http.post<ParcelReturn>(`/orders/sales/${id}/return/accept`)
    return data
  }

  /** The seller refuses it, with a reason the buyer reads - and may take to staff. */
  static async refuseSaleReturn(id: string, reason: string): Promise<ParcelReturn> {
    const { data } = await http.post<ParcelReturn>(`/orders/sales/${id}/return/refuse`, { reason })
    return data
  }

  /** The parcel came back: the buyer is refunded and the units go back on the shelf. */
  static async receiveSaleReturn(id: string): Promise<ParcelReturn> {
    const { data } = await http.post<ParcelReturn>(`/orders/sales/${id}/return/received`)
    return data
  }

  /** The seller's money per currency (specs/037). The token says whose. */
  static async balance(): Promise<Balance[]> {
    const { data } = await http.get<Balance[]>('/orders/sales/balance')
    return data
  }

  /** The payouts made to the seller, newest first. */
  static async payouts(page: number, pageSize: number): Promise<PayoutPage> {
    const { data } = await http.get<PayoutPage>(`/orders/sales/payouts?page=${page}&pageSize=${pageSize}`)
    return data
  }
}
