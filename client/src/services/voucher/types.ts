/** What a voucher gives (specs/069). */
export const BENEFITS = ['Percent', 'FixedAmount', 'FreeShipping'] as const
export type Benefit = (typeof BENEFITS)[number]

/** A voucher's money in one currency - never converted to another (specs/022). */
export interface VoucherAmount {
  currency: string
  /** What a FixedAmount voucher takes off. */
  fixedValue: number | null
  /** The most a percentage or free delivery takes off; null for no cap. */
  maxDiscount: number | null
  /** The least what it applies to must come to; null for none. */
  minSubtotal: number | null
}

export interface VoucherCondition {
  type: 'NewCustomer' | 'FirstOrderInShop' | 'MinQuantity' | string
  value: number | null
}

export interface VoucherTarget {
  type: 'Product' | 'Variant' | string
  id: string
}

/** A voucher as its owner sees it: the platform's for an administrator, a seller's own for them. */
export interface VoucherSummary {
  id: string
  code: string
  name: string
  isPlatform: boolean
  benefit: Benefit | string
  percent: number | null
  status: 'Active' | 'Disabled' | string
  startsAt: string
  endsAt: string | null
  totalLimit: number | null
  usedCount: number
  perCustomerLimit: number | null
  amounts: VoucherAmount[]
  conditions: VoucherCondition[]
  targets: VoucherTarget[]
  createdAt: string
  /** Shown to shoppers (specs/114), or code only. */
  isPublic?: boolean
}

export interface VoucherPage {
  items: VoucherSummary[]
  page: number
  pageSize: number
  totalCount: number
}

/**
 * What creating a voucher sends. No owner: an administrator's is the platform's and a seller's is theirs, and
 * the server reads which from the token (Constitution IV).
 */
export interface NewVoucher {
  code: string
  name: string
  benefit: Benefit
  percent: number | null
  startsAt: string | null
  endsAt: string | null
  totalLimit: number | null
  perCustomerLimit: number | null
  amounts: VoucherAmount[]
  conditions: VoucherCondition[]
  targets: VoucherTarget[]
  /** Shown to shoppers (specs/114); code only when false. */
  isPublic: boolean
}

/**
 * What correcting a voucher sends (specs/113): each term that may change, as its new value. Nothing about what it takes
 * off, its code, targets or start - those never change, so there is no field for them.
 */
export interface VoucherEdit {
  name: string
  endsAt: string | null
  totalLimit: number | null
  perCustomerLimit: number | null
  minSubtotals: { currency: string; minSubtotal: number | null }[]
  minQuantity: number | null
  /** Shown to shoppers or code only (specs/114). */
  isPublic: boolean
}

/** Whose public vouchers to list (specs/114): the platform's and/or shops', optionally for one product. */
export interface PublicVoucherScope {
  platform?: boolean
  sellerIds?: string[]
  productId?: string
  variantIds?: string[]
}

/** A voucher as a shopper sees it listed: what it gives in the currency being browsed in - never a count (specs/114). */
export interface PublicVoucher {
  code: string
  name: string
  isPlatform: boolean
  sellerId: string | null
  benefit: Benefit | string
  percent: number | null
  currency: string
  fixedValue: number | null
  maxDiscount: number | null
  minSubtotal: number | null
  endsAt: string | null
  conditions: VoucherCondition[]
  targeted: boolean
}
