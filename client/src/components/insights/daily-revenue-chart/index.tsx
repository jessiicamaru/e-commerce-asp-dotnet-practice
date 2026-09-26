import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { RevenueDay } from '@/services/insights/types'
import { cn, money } from '@/utils/shared'

/**
 * Revenue per day in one currency (specs/047; a seller's own too since specs/068): one bar per day of the period on a shared baseline, a day
 * with nothing sold left empty, the peak named above and the first and last day named below. Hovering or
 * focusing a bar says the day, the amount and the orders.
 */
export function DailyRevenueChart({ days, rows, currency }: { days: string[]; rows: RevenueDay[]; currency: string }) {
  const { t, i18n } = useTranslation('common')
  const [active, setActive] = useState<number | null>(null)
  const byDay = new Map(rows.filter((r) => r.currency === currency).map((r) => [r.day, r]))
  const peak = Math.max(0, ...days.map((d) => byDay.get(d)?.revenue ?? 0))
  const label = (day: string) =>
    new Date(`${day}T00:00:00Z`).toLocaleDateString(i18n.language, { day: 'numeric', month: 'short', timeZone: 'UTC' })
  const describe = (day: string) => {
    const row = byDay.get(day)
    return row
      ? `${label(day)}: ${money(row.revenue, currency)} · ${t('insights.orders', { count: row.orders })}`
      : `${label(day)}: ${t('insights.noSales')}`
  }

  return (
    <figure className="grid gap-1.5">
      <figcaption className="flex items-baseline justify-between gap-2 text-sm">
        <span className="font-medium">{t('insights.daily')}</span>
        {peak > 0 && (
          <span className="text-muted-foreground text-xs tabular-nums">{t('insights.peak', { amount: money(peak, currency) })}</span>
        )}
      </figcaption>

      <div className="relative">
        {/* The tooltip, above the bar it is about: a fixed box, so moving along the bars never reflows the page. */}
        {active !== null && (
          <div
            role="status"
            className={cn(
              'bg-popover text-popover-foreground ring-border/60 pointer-events-none absolute -top-2 z-10 -translate-y-full rounded-xl px-2.5 py-1.5 text-xs whitespace-nowrap shadow-md ring-1',
              // Centred over its bar, except near either end, where a centred box would leave the card.
              edge(active, days.length) === 'middle' && '-translate-x-1/2',
            )}
            style={
              edge(active, days.length) === 'end'
                ? { right: 0 }
                : { left: edge(active, days.length) === 'start' ? 0 : `${((active + 0.5) / days.length) * 100}%` }
            }
          >
            {describe(days[active])}
          </div>
        )}

        <ol
          className="border-border flex h-36 items-end gap-[2px] border-b"
          aria-label={t('insights.daily')}
          onMouseLeave={() => setActive(null)}
        >
          {days.map((day, index) => {
            const revenue = byDay.get(day)?.revenue ?? 0
            return (
              <li
                key={day}
                tabIndex={0}
                aria-label={describe(day)}
                onMouseEnter={() => setActive(index)}
                onFocus={() => setActive(index)}
                onBlur={() => setActive(null)}
                // The whole column is the hit target, not just the bar - a thin bar is hard to find with a pointer.
                className="group flex h-full min-w-0 flex-1 items-end outline-none"
              >
                {revenue > 0 && (
                  <span
                    className="bg-primary group-hover:bg-primary/80 group-focus-visible:ring-ring/50 block w-full rounded-t-[4px] group-focus-visible:ring-2"
                    // Never thinner than 2% of the height, so a small day next to a big one is still visibly a sale.
                    style={{ height: `${Math.max(2, (revenue / peak) * 100)}%` }}
                  />
                )}
              </li>
            )
          })}
        </ol>
      </div>

      <div className="text-muted-foreground flex justify-between text-xs tabular-nums">
        <span>{label(days[0])}</span>
        <span>{label(days[days.length - 1])}</span>
      </div>
    </figure>
  )
}

/** Which part of the chart a bar sits in, for placing its tooltip. */
function edge(index: number, count: number): 'start' | 'middle' | 'end' {
  const at = (index + 0.5) / count
  return at < 0.2 ? 'start' : at > 0.8 ? 'end' : 'middle'
}
