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
  /** The shop that sold it, frozen at purchase (specs/036). Null: the shop's own, or not recorded. */
  sellerName?: string | null
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
  /** Who sends it (specs/036); null when not recorded - an older order, or a name nobody had yet. */
  sellerName: string | null
  /** The shop's own parcel: named in the reader's language here, not frozen on the server. */
  isShop: boolean
  /** The parcel's id - what the customer names when they say it arrived (specs/040). */
  id?: string
  /** When it was confirmed as received; null until then. */
  deliveredAt?: string | null
  /** 'Customer', or 'Auto' when nobody confirmed it within the period after shipping. */
  deliveryConfirmedBy?: string | null
  /** Its return, once one was asked for (specs/066); a parcel has at most one. */
  return?: ParcelReturn | null
}

/** Where a return has got to (specs/066), as the server names it. */
export type ReturnStatus = 'Requested' | 'Accepted' | 'Refused' | 'Escalated' | 'Rejected' | 'SentBack' | 'Received'

/**
 * One parcel's return (specs/066): asked for by the buyer within the window of its delivery, answered by its
 * seller (staff for the shop's own), taken to staff when refused, sent back, and received - which refunds it.
 */
export interface ParcelReturn {
  id: string
  orderId: string
  shipmentId: string
  /** The shop's own parcel - staff answer it; otherwise its seller does until it is escalated. */
  isShop: boolean
  status: ReturnStatus
  /** The buyer's words. */
  reason: string
  /** Why it was refused or rejected; null when accepted or not decided yet. */
  decisionReason: string | null
  /** The buyer's reference for the parcel sent back. */
  trackingReference: string | null
  requestedAt: string
  /** The latest decision: the seller's, then staff's final word on an escalation. */
  decidedAt: string | null
  sentBackAt: string | null
  receivedAt: string | null
  /** Goods plus their tax, never the delivery - set once received, in the order's own currency. */
  refundAmount: number | null
}

export interface ReturnPage {
  items: ParcelReturn[]
  page: number
  pageSize: number
  totalCount: number
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
  /** Who cancelled it (specs/039): 'Customer' or 'Staff'; null unless cancelled. */
  cancelledBy?: string | null
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
  /**
   * What the shop owes the seller for this sale (specs/037), frozen at checkout: goods before tax, less
   * the marketplace's commission, plus their share of the delivery charge. All four are null on an
   * order placed before that, whose terms were never recorded - never a zero.
   */
  goodsTotal: number | null
  commission: number | null
  shippingShare: number | null
  payout: number | null
  /** Whether a payout has covered it. */
  paidOut: boolean
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
  /** When their parcel was confirmed as received (specs/040); until then its money is on the way. */
  deliveredAt?: string | null
  /** Their parcel's return, if the buyer asked for one (specs/066). */
  return?: ParcelReturn | null
  /**
   * What the shop owes the seller for this sale (specs/037), frozen at checkout: goods before tax, less
   * the marketplace's commission, plus their share of the delivery charge. All four are null on an
   * order placed before that, whose terms were never recorded - never a zero.
   */
  goodsTotal: number | null
  commission: number | null
  shippingShare: number | null
  payout: number | null
  /** Whether a payout has covered it. */
  paidOut: boolean
}

/**
 * A seller's money in one currency (specs/037). Only paid orders count, and only those placed once the
 * terms were being recorded.
 */
export interface Balance {
  currency: string
  /** Paid by the customer, their parcel not sent yet. */
  onTheWay: number
  /** Sent, not paid out yet. */
  due: number
  paidOut: number
}

/** One settlement of what was due, recorded by the shop. Payment is a stub: no money moved. */
export interface Payout {
  id: string
  sellerId: string
  currency: string
  amount: number
  partCount: number
  createdAt: string
}

export interface PayoutPage {
  items: Payout[]
  page: number
  pageSize: number
  totalCount: number
}
