import type { Totals } from '@/services/order/types'
import { money } from '@/utils/shared'

/**
 * The named parts of a total, as stored on the order or quoted for it (specs/012).
 *
 * Prices exclude tax (ADR-002), which is why tax is a line of its own with the rate that produced it.
 */
export function OrderTotals({ totals, shippingName }: { totals: Totals; shippingName?: string }) {
  const rate = totals.taxRate === null ? '' : ` (${+(totals.taxRate * 100).toFixed(2)}%)`

  return (
    <dl className="my-4 grid max-w-sm grid-cols-[1fr_auto] gap-x-8 gap-y-1 text-sm">
      {totals.subtotal !== null && (
        <>
          <dt>Items</dt>
          <dd className="text-right">{money(totals.subtotal)}</dd>
        </>
      )}
      {totals.shippingPrice !== null && (
        <>
          <dt>Delivery{shippingName ? ` · ${shippingName}` : ''}</dt>
          <dd className="text-right">{money(totals.shippingPrice)}</dd>
        </>
      )}
      {totals.taxTotal !== null && (
        <>
          <dt>Tax{rate}</dt>
          <dd className="text-right">{money(totals.taxTotal)}</dd>
        </>
      )}
      {!!totals.discountTotal && (
        <>
          <dt>Discount</dt>
          <dd className="text-right">-{money(totals.discountTotal)}</dd>
        </>
      )}
      <dt className="border-t pt-1 font-semibold">Total</dt>
      <dd className="border-t pt-1 text-right font-semibold">{money(totals.totalAmount)}</dd>
    </dl>
  )
}
