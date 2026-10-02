import { useTranslation } from 'react-i18next'
import { Price } from '@/components/shared/price'
import type { OrderLine } from '@ecommerce/core/services/order/types'

/**
 * The lines of an order or of a quote: the price is frozen on the order line (specs/009), and so is
 * the currency it is in (specs/022) - which is why the currency is passed down rather than read from
 * whatever the shopper is browsing in.
 *
 * <p>
 * Two columns at every width (specs/118, #239): the details, which may shrink and wrap, with "quantity × price" under
 * them, and the total, which never wraps. It was a three-column table, and a table cannot give up a column: in the
 * checkout summary and on a phone the middle column squeezed the name into six lines and pushed the total past the
 * edge, cut to "₫1,250,00".
 * </p>
 */
export function OrderLines({ items, currency }: { items: OrderLine[]; currency?: string }) {
  const { t } = useTranslation('orders')

  return (
    <ul className="divide-y text-sm">
      {items.map((item) => (
        <li key={item.variantId ?? item.productId} className="grid grid-cols-[minmax(0,1fr)_auto] gap-x-4 py-3">
          {/* Long words - a SKU has no spaces - break rather than widen the line. */}
          <div className="min-w-0 [overflow-wrap:anywhere]">
            <div>{item.productName}</div>
            {item.optionSummary && <div className="text-muted-foreground text-xs">{item.optionSummary}</div>}
            {item.sku && <div className="text-muted-foreground text-xs">SKU {item.sku}</div>}
            {/* Who sold it (specs/036) - so an order from one seller says so, parcels or not. */}
            {item.sellerName && (
              <div className="text-muted-foreground text-xs">{t('soldBy', { shop: item.sellerName })}</div>
            )}
            <div className="text-muted-foreground mt-1" data-testid="order-line-quantity">
              {item.quantity} × <Price value={item.unitPrice} currency={currency} />
            </div>
          </div>
          <div className="text-right whitespace-nowrap" data-testid="order-line-total">
            <Price value={item.totalPrice} currency={currency} />
            {/* What vouchers took off this line (specs/070), under its price. */}
            {!!item.discount && (
              <div className="text-xs text-emerald-700 dark:text-emerald-400">
                -<Price value={item.discount} currency={currency} />
              </div>
            )}
          </div>
        </li>
      ))}
    </ul>
  )
}
