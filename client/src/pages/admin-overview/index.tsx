import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { InsightPanel as Panel } from '@/components/insights/insight-panel'
import { PeriodPicker } from '@/components/insights/period-picker'
import { RankedList as Ranked } from '@/components/insights/ranked-list'
import { RevenuePanel } from '@/components/insights/revenue-panel'
import { PageTitle } from '@/components/seller/page-title'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { useInsights } from '@/hooks/insights'
import { useReviewQueue } from '@/hooks/moderation'
import { useShopApplications } from '@/hooks/shop-applications'
import type { Period } from '@/services/insights/types'
import { cn, money } from '@/utils/shared'
import { periodDays, periodRange } from '@/utils/insights'

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
  const { from, to } = useMemo(() => periodRange(period), [period])
  const [currency, setCurrency] = useState('VND')

  const data = useInsights(from, to, currency)
  const products = useReviewQueue('Pending', 1, 1)
  const shops = useShopApplications('Pending', 1, 1)
  const stats = data.stats.data
  const emails = new Map((data.people.data ?? []).map((p) => [p.id, p.email]))

  return (
    <section className="grid gap-6">
      <PageTitle title={t('overview.title')} subtitle={t('overview.subtitle')} />

      <PeriodPicker period={period} onChange={setPeriod} />

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
          <RevenuePanel revenue={data.revenue.data} days={periodDays(data.revenue.data.firstDay ?? from, data.revenue.data.lastDay ?? to)} currency={currency} onCurrency={setCurrency} />
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
