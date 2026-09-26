import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { EyeIcon, StarIcon } from 'lucide-react'
import { InsightPanel } from '@/components/insights/insight-panel'
import { PeriodPicker } from '@/components/insights/period-picker'
import { RankedList } from '@/components/insights/ranked-list'
import { RevenuePanel } from '@/components/insights/revenue-panel'
import { PageTitle } from '@/components/seller/page-title'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { useSellerInsights } from '@/hooks/insights'
import type { Period } from '@/services/insights/types'
import { periodDays, periodRange } from '@/utils/insights'
import { money } from '@/utils/shared'

/**
 * How a seller's shop is doing (specs/068, #111): their revenue per currency and per day, what sells, what
 * people look at, and how their products are rated - over the last 7, 30 or 90 days.
 *
 * <p>
 * Every number is THEIRS: the revenue is their own lines on sold orders before tax, never an order's total
 * (specs/034), and a parcel that came back and was refunded is not in it. Two services answer - Order for
 * money, Catalog for views and ratings - and neither is asked who the seller is: the token says.
 * </p>
 */
export function ShopInsightsPage() {
  const { t, i18n } = useTranslation('seller')
  const [period, setPeriod] = useState<Period>(30)
  // Fixed per choice, not per render: a "now" that moved every render would be a new query every time.
  const { from, to } = useMemo(() => periodRange(period), [period])
  const [currency, setCurrency] = useState('VND')
  const { revenue, products, mine } = useSellerInsights(from, to)
  const rating = (average: number | null) =>
    average === null ? '—' : average.toLocaleString(i18n.language, { minimumFractionDigits: 1, maximumFractionDigits: 1 })

  return (
    <section className="grid gap-6">
      <PageTitle title={t('insights.title')} subtitle={t('insights.subtitle')} />
      <PeriodPicker period={period} onChange={setPeriod} />

      <div className="grid grid-cols-2 gap-3">
        <div className="bg-card ring-border/60 grid gap-1 rounded-3xl p-4 ring-1">
          <span className="text-muted-foreground flex items-center gap-1.5 text-xs">
            <EyeIcon className="size-3.5" /> {t('insights.views')}
          </span>
          <span className="text-2xl font-bold tabular-nums">{mine.data ? mine.data.views.toLocaleString(i18n.language) : '…'}</span>
        </div>
        <div className="bg-card ring-border/60 grid gap-1 rounded-3xl p-4 ring-1">
          <span className="text-muted-foreground flex items-center gap-1.5 text-xs">
            <StarIcon className="size-3.5" /> {t('insights.rating')}
          </span>
          {!mine.data ? (
            <span className="text-2xl font-bold">…</span>
          ) : mine.data.ratingAverage === null ? (
            <span className="text-muted-foreground text-sm">{t('insights.noRating')}</span>
          ) : (
            <span className="text-2xl font-bold tabular-nums">
              {rating(mine.data.ratingAverage)}{' '}
              <span className="text-muted-foreground text-sm font-normal">
                {t('insights.reviews', { count: mine.data.ratingCount })}
              </span>
            </span>
          )}
        </div>
      </div>

      <InsightPanel title={t('insights.revenue')}>
        {revenue.isError ? (
          <ErrorMessage>{t('insights.loadFailed', { ns: 'common' })}</ErrorMessage>
        ) : !revenue.data ? (
          <LoadingRows rows={2} />
        ) : revenue.data.totals.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t('insights.noRevenue')}</p>
        ) : (
          <RevenuePanel revenue={revenue.data} days={periodDays(to, period)} currency={currency} onCurrency={setCurrency} />
        )}
      </InsightPanel>

      <div className="grid gap-4 lg:grid-cols-2">
        <InsightPanel title={t('insights.topSelling')}>
          <RankedList
            rows={products.data?.map((p) => ({
              key: p.productId,
              label: (
                <Link to={`/shop/products/${p.productId}`} className="hover:underline">
                  {p.productName}
                </Link>
              ),
              // Units, then the money per currency - never one sum of both.
              value: [t('insights.units', { count: p.units }), ...p.revenue.map((r) => money(r.amount, r.currency))].join(' · '),
            }))}
            failed={products.isError}
          />
        </InsightPanel>
        <InsightPanel title={t('insights.topViewed')}>
          <RankedList
            rows={mine.data?.products.map((p) => ({
              key: p.productId,
              label: (
                <Link to={`/shop/products/${p.productId}`} className="hover:underline">
                  {p.name}
                </Link>
              ),
              value: [
                t('insights.viewCount', { count: p.views }),
                p.ratingCount > 0 ? `★ ${rating(p.ratingAverage)} (${p.ratingCount})` : null,
              ]
                .filter(Boolean)
                .join(' · '),
            }))}
            failed={mine.isError}
          />
        </InsightPanel>
      </div>
    </section>
  )
}
