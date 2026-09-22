import { useTranslation } from 'react-i18next'
import { currentCurrency } from '@/config/money'
import { money } from '@/utils/shared'

/**
 * An amount of money, or the reason there is not one (specs/022).
 *
 * **The one place that decides what a null price looks like.** A variant nobody has priced in the
 * currency being browsed in has no price in it - not zero, and not the other currency's number. Every
 * price on the storefront goes through here so that the answer is the same wherever it appears, and
 * so that "not sold in USD" reads as something a shopper can act on: switching currency brings it
 * back.
 */
export function Price({
  value,
  currency,
  className,
}: {
  value: number | null | undefined
  /** Which currency `value` is in. An order's own, frozen currency - not necessarily today's choice. */
  currency?: string
  className?: string
}) {
  const { t } = useTranslation()
  const code = currency || currentCurrency()

  if (value === null || value === undefined) {
    return (
      <span className={className}>
        <span className="text-muted-foreground text-sm">{t('currency.notSoldIn', { currency: code })}</span>
      </span>
    )
  }

  return <span className={className}>{money(value, code)}</span>
}
