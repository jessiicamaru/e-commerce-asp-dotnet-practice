import { useTranslation } from 'react-i18next'
import { DailyRevenueChart } from '@/components/insights/daily-revenue-chart'
import type { Revenue } from '@/services/insights/types'
import { cn, money } from '@/utils/shared'

/**
 * Revenue over a period (specs/047, 068): one card per currency - the total, the orders and the average - and
 * the chosen currency's days as bars. The shop's in the administrator's Overview, a seller's own in theirs:
 * the server answers both in one shape, so there is one panel.
 *
 * <p>
 * <b>Money is never added across currencies.</b> Each currency is its own card and, when chosen, its own
 * chart; a total made of dong and dollars would be neither.
 * </p>
 */
export function RevenuePanel({
  revenue,
  days,
  currency,
  onCurrency,
}: {
  revenue: Revenue
  /** Every day of the period, oldest first - the chart draws the empty ones too. */
  days: string[]
  currency: string
  onCurrency: (currency: string) => void
}) {
  const { t } = useTranslation('common')
  const shown = revenue.totals.some((r) => r.currency === currency) ? currency : revenue.totals[0].currency

  return (
    <div className="grid gap-5">
      <div className="grid gap-3 sm:grid-cols-2">
        {revenue.totals.map((total) => (
          <button
            key={total.currency}
            type="button"
            aria-pressed={total.currency === shown}
            onClick={() => onCurrency(total.currency)}
            className={cn(
              'grid gap-1 rounded-3xl p-4 text-left ring-1 transition-colors',
              total.currency === shown ? 'bg-primary/10 ring-primary' : 'ring-border/60 hover:bg-secondary',
            )}
          >
            <span className="text-2xl font-bold tabular-nums">{money(total.revenue, total.currency)}</span>
            <span className="text-muted-foreground text-sm">
              {t('insights.orders', { count: total.orders })} ·{' '}
              {t('insights.average', { amount: money(total.averageOrderValue, total.currency) })}
            </span>
          </button>
        ))}
      </div>

      <DailyRevenueChart days={days} rows={revenue.days} currency={shown} />
    </div>
  )
}
