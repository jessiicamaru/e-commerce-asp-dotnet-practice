import type { AddressFields } from '@/services/address/types'

export interface ShippingOption {
  code: string
  name: string
  /** In `currency`. Only options priced in the request's currency are offered (specs/022). */
  price: number | null
  currency: string
}

export interface OrderLine {
  productId: string
  productName: string
  /** Frozen at purchase (specs/020): what was bought, in the words used then. */
  variantId: string | null
  sku: string | null
  optionSummary: string | null
  quantity: number
  unitPrice: number
  totalPrice: number
  taxAmount: number | null
}

/** The named parts of a total (specs/012): subtotal + shipping + tax - discount = total. */
export interface Totals {
  /**
   * The currency every amount here is in (specs/022). On an order it is FROZEN - an order placed in
   * dong still reads in dong to somebody browsing in dollars, because an order is a record of a
   * purchase. Empty on orders placed before the feature, which were in the shop's default.
   */
  currency: string
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
  /** Frozen at checkout (specs/022): the currency this order was charged in, not today's choice. */
  currency: string
  /** Frozen too (specs/021): the language its lines were worded in. */
  language: string
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
