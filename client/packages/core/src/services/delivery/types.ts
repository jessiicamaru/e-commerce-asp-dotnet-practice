/** The shop's one carrier (specs/098): its name, and its tracking page with `{reference}` where the reference goes. */
export interface Carrier {
  name: string
  trackingUrlTemplate: string | null
}

/** A delivery option as staff edit it: offered or not, in the shoppers' order, a price per currency (none: not offered in it). */
export interface DeliveryOption {
  code: string
  name: string
  isActive: boolean
  sortOrder: number
  prices: Record<string, number>
  /** Business days, soonest and latest - both or neither (specs/134). */
  minDays: number | null
  maxDays: number | null
}

export interface DeliverySettings {
  options: DeliveryOption[]
  carrier: Carrier
}
