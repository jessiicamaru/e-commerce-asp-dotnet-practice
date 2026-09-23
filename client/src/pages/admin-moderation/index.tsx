import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ArrowRightIcon } from 'lucide-react'
import { PageTitle } from '@/components/seller/page-title'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { useMyDecisions, useReviewQueue } from '@/hooks/moderation'
import { useShopApplications } from '@/hooks/shop-applications'

/** How many of their own decisions a moderator sees here; the rest are in the audit log. */
export const RECENT_DECISIONS = 8

/**
 * A moderator's home (specs/045): how much is waiting in each queue, and what they decided lately. The
 * counts are the queues' own totals - one page of one row each - so the dashboard never disagrees with
 * the queue it links to.
 */
export function AdminModerationPage() {
  const { t, i18n } = useTranslation('admin')
  const products = useReviewQueue('Pending', 1, 1)
  const shops = useShopApplications('Pending', 1, 1)
  const recent = useMyDecisions(RECENT_DECISIONS)

  return (
    <section className="grid gap-6">
      <PageTitle title={t('moderation.title')} subtitle={t('moderation.subtitle')} />

      <div className="grid gap-3 sm:grid-cols-2">
        <Waiting label={t('moderation.productsWaiting')} count={products.data?.totalCount} to="/admin/products" />
        <Waiting label={t('moderation.shopsWaiting')} count={shops.data?.totalCount} to="/admin/shops" />
      </div>

      <div className="grid gap-3">
        <h2 className="font-semibold">{t('moderation.recent')}</h2>
        {recent.isError ? (
          <ErrorMessage>{t('moderation.loadFailed')}</ErrorMessage>
        ) : recent.isPending ? (
          <LoadingRows rows={3} />
        ) : recent.data.items.length === 0 ? (
          <p className="bg-card ring-border/60 rounded-3xl p-6 text-sm ring-1">{t('moderation.noneRecent')}</p>
        ) : (
          <ul className="bg-card ring-border/60 divide-border/60 divide-y rounded-3xl ring-1">
            {recent.data.items.map((entry) => (
              <li key={entry.id} className="grid gap-0.5 px-5 py-3">
                <p className="text-sm font-medium">
                  {t(`audit.action.${entry.action}`, { defaultValue: entry.action })}
                </p>
                <p className="text-muted-foreground text-sm">{entry.summary}</p>
                <p className="text-muted-foreground text-xs">{new Date(entry.occurredAt).toLocaleString(i18n.language)}</p>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}

function Waiting({ label, count, to }: { label: string; count: number | undefined; to: string }) {
  const { t } = useTranslation('admin')

  return (
    <Link to={to} className="bg-card ring-border/60 hover:ring-primary grid gap-1 rounded-3xl p-5 ring-1 transition-shadow">
      <span className="text-muted-foreground text-sm">{label}</span>
      <span className="text-3xl font-bold tabular-nums">{count ?? '…'}</span>
      <span className="text-primary flex items-center gap-1 text-sm font-medium">
        {t('moderation.open')} <ArrowRightIcon className="size-4" />
      </span>
    </Link>
  )
}
