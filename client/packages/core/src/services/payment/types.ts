/** What Payment says about itself through `/health` (specs/134, specs/143). */
export type PaymentAbout = {
  /** False for the stub and VNPay's sandbox - the checkout says no money is moved; null when Payment cannot be asked. */
  movesMoney: boolean | null
  /** The customer pays on a gateway's page after placing the order (VNPay), rather than when placing it. */
  atGateway: boolean
}

export type PaymentCheckoutState = 'Preparing' | 'AwaitingPayment' | 'Expired' | 'Paid' | 'Failed'

/** How an order's payment stands, for its owner (specs/143). */
export type PaymentCheckout = {
  provider: string
  state: PaymentCheckoutState
  /** A freshly signed link to the gateway, only while it waits. */
  payUrl: string | null
  expiresAt: string | null
}
