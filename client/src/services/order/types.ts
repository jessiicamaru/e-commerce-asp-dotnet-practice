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

/**
 * One parcel of an order (specs/035): the goods of one seller, or the shop's own. `status` is `Paid`
 * while nobody has started it, then `Preparing`, then `Shipped` with its tracking reference.
 */
export interface Shipment {
  status: string
  trackingReference: string | null
  /** What is in it, in the words frozen on the order's lines. */
  items: string[]
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
  /** The parcel's, when there is exactly one; null when the order goes in several (see `shipments`). */
  trackingReference: string | null
  shipments: Shipment[] | null
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
  /** How many parcels it goes in, and how many have gone (specs/035). */
  shipmentCount: number
  shipmentsShipped: number
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
  /** THEIR part's state (specs/035): Paid (waiting), Preparing or Shipped - not the order's. */
  status: string
  createdAt: string
  updatedAt: string
  items: OrderLine[]
  subtotal: number
  currency: string
  language: string
  /** Their parcel's, once shipped. */
  trackingReference: string | null
  /** Where to send it - present ONLY while their part is waiting or being prepared (research D6). */
  shippingAddress: AddressFields | null
}
