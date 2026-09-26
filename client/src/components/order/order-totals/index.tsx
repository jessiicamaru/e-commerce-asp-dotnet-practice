import { useTranslation } from 'react-i18next'
import { Price } from '@/components/shared/price'
import type { Totals } from '@/services/order/types'

/**
 * The named parts of a total, as stored on the order or quoted for it (specs/012).
 *
 * Prices exclude tax (ADR-002), which is why tax is a line of its own with the rate that produced it.
 *
 * Shown in the **order's own** currency (specs/022), which on a finished order is the one it was
 * placed in - not whatever the reader happens to be browsing in now.
 */
export function OrderTotals({ totals, shippingName }: { totals: Totals; shippingName?: string }) {
  const { t } = useTranslation('checkout')
  const rate = totals.taxRate === null ? '' : ` (${+(totals.taxRate * 100).toFixed(2)}%)`

  return (
    <dl className="my-4 grid max-w-sm grid-cols-[1fr_auto] gap-x-8 gap-y-1 text-sm">
      {totals.subtotal !== null && (
        <>
          <dt>{t('totals.items')}</dt>
          <dd className="text-right">
            <Price value={totals.subtotal} currency={totals.currency} />
          </dd>
        </>
      )}
      {totals.shippingPrice !== null && (
        <>
          <dt>
            {t('totals.delivery')}
            {shippingName ? ` · ${shippingName}` : ''}
          </dt>
          <dd className="text-right">
            <Price value={totals.shippingPrice} currency={totals.currency} />
          </dd>
        </>
      )}
      {totals.taxTotal !== null && (
        <>
          <dt>
            {t('totals.tax')}
            {rate}
          </dt>
          <dd className="text-right">
            <Price value={totals.taxTotal} currency={totals.currency} />
          </dd>
        </>
      )}
      {/* Each voucher by its code, and whose for a shop's (specs/070); the one discount row when there are none -
          an order from before vouchers, whose discount was always nothing. */}
      {totals.vouchers && totals.vouchers.length > 0
        ? totals.vouchers.map((voucher) => (
            <div key={voucher.code} className="contents text-emerald-700 dark:text-emerald-400">
              <dt>
                {t('totals.voucher', { code: voucher.code })}
                {voucher.isShop && voucher.sellerName ? ` · ${voucher.sellerName}` : ''}
              </dt>
              <dd className="text-right">
                -<Price value={voucher.amount} currency={totals.currency} />
              </dd>
            </div>
          ))
        : !!totals.discountTotal && (
            <>
              <dt>{t('totals.discount')}</dt>
              <dd className="text-right">
                -<Price value={totals.discountTotal} currency={totals.currency} />
              </dd>
            </>
          )}
      <dt className="border-t pt-1 font-semibold">{t('totals.total')}</dt>
      <dd className="border-t pt-1 text-right font-semibold">
        <Price value={totals.totalAmount} currency={totals.currency} />
      </dd>
    </dl>
  )
}
