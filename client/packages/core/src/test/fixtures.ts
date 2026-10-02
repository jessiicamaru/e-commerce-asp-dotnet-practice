/**
 * A sale's earnings when the test is not about them (specs/037): not recorded, as on an order placed
 * before commission and delivery shares were. Tests about money set their own numbers.
 */
export const noEarnings = {
  goodsTotal: null,
  commission: null,
  shippingShare: null,
  payout: null,
  paidOut: false,
} as const
