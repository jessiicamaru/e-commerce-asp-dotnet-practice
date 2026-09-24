import { useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { PageTitle } from '@/components/seller/page-title'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { useInsights } from '@/hooks/insights'
import { useReviewQueue } from '@/hooks/moderation'
import { useShopApplications } from '@/hooks/shop-applications'
import { PERIODS, type Period, type Revenue } from '@/services/insights/types'
import { cn, money } from '@/utils/shared'
import { periodDays } from '@/utils/insights'
import { DailyRevenueChart } from './daily-chart'

/**
 * How the shop is doing (specs/047), for administrators: people, what waits for review, revenue per
 * currency and per day, and the top sellers, most viewed and biggest buyers over a chosen period.
 *
 * <p>
 * <b>Money is never added across currencies.</b> Each currency has its own total and its own chart; a
 * dong total next to a dollar one is two facts, and one number made of both would be neither.
 * </p>
 */
export function AdminOverviewPage() {
  const { t } = useTranslation('admin')
  const [period, setPeriod] = useState<Period>(30)
  // Fixed per choice, not per render: a "now" that moved every render would be a new query every time.
  // The server counts WHOLE UTC days, both ends included (specs/055): today and the period - 1 days before
  // it are exactly the days the chart draws. Asking from period x 24 h ago touched one day more (#125).
  const { from, to } = useMemo(() => {
    const end = new Date()
    return { from: new Date(end.getTime() - (period - 1) * 86_400_000).toISOString(), to: end.toISOString() }
  }, [period])
  const [currency, setCurrency] = useState('VND')

  const data = useInsights(from, to, currency)
  const products = useReviewQueue('Pending', 1, 1)
  const shops = useShopApplications('Pending', 1, 1)
  const stats = data.stats.data
  const emails = new Map((data.people.data ?? []).map((p) => [p.id, p.email]))

  return (
    <section className="grid gap-6">
      <PageTitle title={t('overview.title')} subtitle={t('overview.subtitle')} />

      <div className="bg-card ring-border/60 flex gap-1 justify-self-start rounded-full p-1 ring-1">
        {PERIODS.map((p) => (
          <Button
            key={p}
            size="sm"
            variant={p === period ? 'default' : 'ghost'}
            className="rounded-full"
            aria-pressed={p === period}
            onClick={() => setPeriod(p)}
          >
            {t('overview.period', { count: p })}
          </Button>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-6">
        <Stat label={t('overview.people.customers')} value={stats?.customers} />
        <Stat label={t('overview.people.sellers')} value={stats?.sellers} />
        <Stat label={t('overview.people.moderators')} value={stats?.moderators} />
        <Stat label={t('overview.people.stopped')} value={stats ? stats.locked + stats.banned : undefined} />
        <Stat label={t('overview.waiting.products')} value={products.data?.totalCount} to="/admin/products" />
        <Stat label={t('overview.waiting.shops')} value={shops.data?.totalCount} to="/admin/shops" />
      </div>

      <Panel title={t('overview.revenue')}>
        {data.revenue.isError ? (
          <ErrorMessage>{t('overview.loadFailed')}</ErrorMessage>
        ) : !data.revenue.data ? (
          <LoadingRows rows={2} />
        ) : data.revenue.data.totals.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t('overview.noRevenue')}</p>
        ) : (
          <RevenuePanel revenue={data.revenue.data} days={periodDays(to, period)} currency={currency} onCurrency={setCurrency} />
        )}
      </Panel>

      <div className="grid gap-4 lg:grid-cols-3">
        <Panel title={t('overview.topSelling')}>
          <Ranked
            rows={data.products.data?.map((p) => ({
              key: p.productId,
              label: <Link to={`/products/${p.productId}`} className="hover:underline">{p.productName}</Link>,
              value: t('overview.units', { count: p.units }),
            }))}
            failed={data.products.isError}
          />
        </Panel>
        <Panel title={t('overview.topViewed')}>
          <Ranked
            rows={data.viewed.data?.map((v) => ({
              key: v.productId,
              label: <Link to={`/products/${v.productId}`} className="hover:underline">{v.name}</Link>,
              value: t('overview.views', { count: v.views }),
            }))}
            failed={data.viewed.isError}
          />
        </Panel>
        <Panel title={t('overview.topBuyers')}>
          <Ranked
            rows={data.buyers.data?.map((b) => ({
              key: b.customerId,
              label: <span className="break-all">{emails.get(b.customerId) ?? b.customerId.slice(0, 8)}</span>,
              value: b.spent.map((s) => money(s.amount, s.currency)).join(' · '),
            }))}
            failed={data.buyers.isError}
          />
        </Panel>
      </div>
    </section>
  )
}

function RevenuePanel({
  revenue,
  days,
  currency,
  onCurrency,
}: {
  revenue: Revenue
  days: string[]
  currency: string
  onCurrency: (c: string) => void
}) {
  const { t } = useTranslation('admin')
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
              {t('overview.orders', { count: total.orders })} · {t('overview.average', { amount: money(total.averageOrderValue, total.currency) })}
            </span>
          </button>
        ))}
      </div>

      <DailyRevenueChart days={days} rows={revenue.days} currency={shown} />
    </div>
  )
}

function Stat({ label, value, to }: { label: string; value: number | undefined; to?: string }) {
  const body = (
    <>
      <span className="text-muted-foreground text-xs">{label}</span>
      <span className="text-2xl font-bold tabular-nums">{value ?? '…'}</span>
    </>
  )
  const style = 'bg-card ring-border/60 grid gap-1 rounded-3xl p-4 ring-1'
  return to ? (
    <Link to={to} className={cn(style, 'hover:ring-primary transition-shadow')}>
      {body}
    </Link>
  ) : (
    <div className={style}>{body}</div>
  )
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="bg-card ring-border/60 grid content-start gap-4 rounded-3xl p-5 ring-1">
      <h2 className="font-semibold">{title}</h2>
      {children}
    </div>
  )
}

function Ranked({ rows, failed }: { rows?: { key: string; label: ReactNode; value: string }[]; failed: boolean }) {
  const { t } = useTranslation('admin')
  if (failed) return <ErrorMessage>{t('overview.loadFailed')}</ErrorMessage>
  if (!rows) return <LoadingRows rows={3} />
  if (rows.length === 0) return <p className="text-muted-foreground text-sm">{t('overview.none')}</p>

  return (
    <ol className="grid gap-2 text-sm">
      {rows.map((row, index) => (
        <li key={row.key} className="grid grid-cols-[1.5rem_1fr_auto] items-baseline gap-2">
          <span className="text-muted-foreground tabular-nums">{index + 1}</span>
          <span className="min-w-0 truncate">{row.label}</span>
          <span className="text-muted-foreground text-xs tabular-nums">{row.value}</span>
        </li>
      ))}
    </ol>
  )
}
