import { api } from './http'
import type { AddressFields } from './addresses'

// Checkout and orders (#38, #39). The customer chooses only where and how; what is bought comes from
// their cart, what it costs from Catalog, Order's delivery options and the destination's tax rate.

export interface ShippingOption {
  code: string
  name: string
  price: number
}

export interface OrderLine {
  productId: string
  productName: string
  quantity: number
  unitPrice: number
  totalPrice: number
  taxAmount: number | null
}

/** The named parts of a total (feature 012): subtotal + shipping + tax - discount = total. */
export interface Totals {
  subtotal: number | null
  shippingPrice: number | null
  taxTotal: number | null
  discountTotal: number | null
  taxRate: number | null
  totalAmount: number
}

export interface Quote extends Totals {
  items: OrderLine[]
  shippingAddress: AddressFields
  shippingOption: { code: string; name: string }
}

export interface Order extends Totals {
  orderId: string
  status: string
  failureReason: string | null
  createdAt: string
  updatedAt: string
  items: OrderLine[]
  shippingAddress: AddressFields | null
  shippingOption: { code: string; name: string } | null
  trackingReference: string | null
}

export interface OrderSummary {
  orderId: string
  totalAmount: number
  status: string
  failureReason: string | null
  itemCount: number
  createdAt: string
  updatedAt: string
}

export interface OrderPage {
  items: OrderSummary[]
  page: number
  pageSize: number
  totalCount: number
}

export interface Choice {
  addressId: string | null
  shippingOption: string
}

export const listShippingOptions = () => api<ShippingOption[]>('/orders/shipping-options', { anonymous: true })

export function getQuote({ addressId, shippingOption }: Choice): Promise<Quote> {
  const params = new URLSearchParams({ shippingOption })
  if (addressId) params.set('addressId', addressId)
  return api<Quote>(`/orders/quote?${params}`)
}

export const placeOrder = (choice: Choice) => api<{ orderId: string }>('/orders', { method: 'POST', body: choice })

export const getOrder = (id: string) => api<Order>(`/orders/${id}`)

export const listMyOrders = (page: number, pageSize: number) =>
  api<OrderPage>(`/orders?page=${page}&pageSize=${pageSize}`)

/** Still waiting on the saga: stock is being reserved and payment taken. */
export const isSettling = (status: string) => status === 'Submitted'

/**
 * An order's status as a sentence a customer understands (#39). The saga's failure reasons are written
 * for operators ("Insufficient stock for product 01a0..."), so they are classified, not shown raw.
 */
export function describeStatus(status: string, failureReason: string | null): string {
  switch (status) {
    case 'Submitted':
      return 'We are reserving your items and taking payment…'
    case 'Paid':
      return 'Paid. We will start preparing it soon.'
    case 'Preparing':
      return 'Being prepared for dispatch.'
    case 'Shipped':
      return 'On its way.'
    case 'Failed':
      if (failureReason && /stock/i.test(failureReason))
        return 'Not placed: some items ran out of stock. Nothing was charged, and your cart is unchanged.'
      if (failureReason && /payment|declin/i.test(failureReason))
        return 'Not placed: the payment was declined. Your cart is unchanged, so you can try again.'
      return 'Not placed. Nothing was charged, and your cart is unchanged.'
    default:
      return status
  }
}
