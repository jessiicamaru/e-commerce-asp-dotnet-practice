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

/**
 * One order holding at least one of the signed-in seller's lines (specs/034).
 *
 * Every amount is over the SELLER'S lines only. There is deliberately no order total here: on an
 * order mixing sellers it includes goods that are not theirs, and delivery and tax are computed over
 * the whole order.
 */
export interface SaleSummary {
  orderId: string
  /** `Paid`, `Preparing` or `Shipped`. A failed or still-settling order is never a sale. */
  status: string
  createdAt: string
  updatedAt: string
  lineCount: number
  units: number
  /** Before tax, in `currency` - the order's own, frozen at checkout. */
  subtotal: number
  currency: string
}

export interface SalePage {
  items: SaleSummary[]
  page: number
  pageSize: number
  totalCount: number
}

/**
 * One sale: the seller's own lines and nothing else. No customer, no address, no order total and no
 * tracking reference - a seller who only looks has no use for them (specs/034 research D4).
 */
export interface Sale {
  orderId: string
  status: string
  createdAt: string
  updatedAt: string
  items: OrderLine[]
  subtotal: number
  currency: string
  language: string
}
