import type { AddressFields } from '@/services/address/types'

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

/** The named parts of a total (specs/012): subtotal + shipping + tax - discount = total. */
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

/** What the customer chooses at checkout: where, and how. Nothing the server owns. */
export interface CheckoutChoice {
  addressId: string | null
  shippingOption: string
}
