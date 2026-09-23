import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { stockLevel } from '@/utils/stock'
import { cn } from '@/utils/shared'

const TONE = {
  in: 'border-emerald-600/40 text-emerald-700 dark:text-emerald-400',
  low: 'border-amber-500/50 text-amber-700 dark:text-amber-400',
  out: 'border-destructive/40 text-destructive',
  unknown: 'text-muted-foreground',
} as const

/**
 * How many are left, as a number with a colour: Inventory's count, not Catalog's in-stock flag.
 *
 * <p>
 * <b>"Not yet known" is its own state</b>, and it is not zero: a freshly listed variant has no stock
 * row until Inventory hears of it through the broker (specs/031). Drawing it as 0 would tell a seller
 * their new listing is sold out.
 * </p>
 * <p>
 * `reserved` is said separately because "I have 3 but only 1 is available" reads as a bug until you
 * know two are inside somebody's checkout.
 * </p>
 */
export function StockBadge({
  available,
  reserved = 0,
  pending = false,
  className,
}: {
  available: number | null | undefined
  reserved?: number
  pending?: boolean
  className?: string
}) {
  const { t } = useTranslation('catalog')
  const level = pending ? 'unknown' : stockLevel(available)

  const label = pending
    ? t('stock.checking')
    : level === 'unknown'
      ? t('stock.unknown')
      : level === 'out'
        ? t('stock.out')
        : level === 'low'
          ? t('stock.low', { count: available! })
          : t('stock.in', { count: available! })

  return (
    <span className={cn('inline-flex flex-wrap items-center gap-1.5', className)}>
      <Badge variant="outline" className={TONE[level]}>
        {label}
      </Badge>
      {reserved > 0 && <span className="text-muted-foreground text-xs">{t('stock.reserved', { count: reserved })}</span>}
    </span>
  )
}
