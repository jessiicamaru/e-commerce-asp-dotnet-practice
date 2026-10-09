import { useTranslation } from 'react-i18next'
import { cn } from 'cn'
import { currentCurrency } from '@ecommerce/core/config/money'
import { money } from '@ecommerce/core/utils/shared'
import { percentOff } from './percent-off'

/**
 * What a price is compared against, struck through, with the percentage off (specs/161, #369) - drawn beside a
 * `Price`, and nothing at all when the price is not reduced. The server keeps a compare-at above its price, and in the
 * same currency, so this only words it.
 */
export function CompareAt({
  price,
  compareAt,
  currency,
  className,
}: {
  price: number | null | undefined
  compareAt: number | null | undefined
  currency?: string
  className?: string
}) {
  const { t } = useTranslation()
  const percent = percentOff(price, compareAt)
  if (percent === null) return null

  return (
    <span className={cn('inline-flex items-baseline gap-1.5', className)}>
      <s className="text-muted-foreground text-[0.85em]" aria-label={t('compareAt.was', { price: money(compareAt!, currency || currentCurrency()) })}>
        {money(compareAt!, currency || currentCurrency())}
      </s>
      <span className="text-destructive text-[0.8em] font-semibold">{t('compareAt.off', { percent })}</span>
    </span>
  )
}
