import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ProductImage } from '@/components/product/product-image'
import { useProduct } from '@/hooks/product'
import type { OrderLinePreview } from '@/services/order/types'
import { orderReference } from '@/utils/order'
import { money } from '@/utils/shared'
import { OrderStatusChip } from '../order-status-chip'

/**
 * One order in a list (specs/132, #248) - the customer's orders, a seller's sales, the staff queues: the first
 * product's picture, the short reference the notices use, up to three product names, the date, a status chip and the
 * amount, the whole row one link. A list used to name an order by its timestamp to the second.
 */
export function OrderRow({
  to,
  state,
  orderId,
  lines,
  lineCount,
  createdAt,
  status,
  shipped,
  parcels,
  amount,
  currency,
  detail,
}: {
  to: string
  /** Where the order was opened from (specs/129), for the staff console. */
  state?: unknown
  orderId: string
  lines?: OrderLinePreview[] | null
  /** How many lines in all, for "+ n more". */
  lineCount: number
  createdAt: string
  status: string
  shipped?: number
  parcels?: number
  amount: number
  currency: string
  /** Anything a list adds under the names - who bought it, why it failed. */
  detail?: React.ReactNode
}) {
  const { t, i18n } = useTranslation('orders')
  const names = (lines ?? []).map((line) => line.productName)
  const more = lineCount - names.length

  return (
    <Link
      to={to}
      state={state}
      className="bg-card ring-border/60 hover:ring-primary/60 flex items-center gap-4 rounded-2xl p-3 ring-1 transition-colors"
    >
      <span className="w-16 shrink-0">
        <Thumbnail line={lines?.[0]} />
      </span>
      <span className="grid min-w-0 flex-1 gap-0.5">
        <span className="flex flex-wrap items-center gap-2">
          <span className="font-mono text-sm font-semibold">{orderReference(orderId)}</span>
          <OrderStatusChip status={status} shipped={shipped} parcels={parcels} />
        </span>
        <span className="truncate text-sm">
          {names.length > 0 ? names.join(' · ') : t('itemCount', { count: lineCount })}
          {names.length > 0 && more > 0 && <span className="text-muted-foreground"> {t('row.more', { count: more })}</span>}
        </span>
        <span className="text-muted-foreground text-xs">{new Date(createdAt).toLocaleDateString(i18n.language)}</span>
        {detail}
      </span>
      <span className="shrink-0 text-right text-sm font-semibold">{money(amount, currency)}</span>
    </Link>
  )
}

/** The first line's picture through the product lookup the cart uses; the lens tile for a product since gone. */
function Thumbnail({ line }: { line?: OrderLinePreview }) {
  const product = useProduct(line?.productId ?? '')
  return (
    <ProductImage
      product={{ id: line?.productId ?? 'none', name: line?.productName ?? '', imageUrl: product.data?.imageUrl ?? null }}
      imageUrl={product.data?.variants?.find((v) => v.id === line?.variantId)?.imageUrl}
      thumb
    />
  )
}
