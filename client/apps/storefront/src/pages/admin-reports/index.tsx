import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { TextPrompt } from '@/components/shared/text-prompt'
import { Badge } from '@ecommerce/ui/badge'
import { Button } from '@ecommerce/ui/button'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { useReportActions, useReportQueue } from '@ecommerce/core/hooks/reports'
import type { ReportedItem } from '@ecommerce/core/services/reports/types'

/**
 * What shoppers reported (specs/101), one card per thing, most reported first. Acting is the existing hide or take-down
 * - it closes every report of the thing on the server and tells each reporter - and "no action" dismisses them. Nothing
 * is hidden by the number of reports alone: a person decides.
 */
export function AdminReportsPage() {
  const { t } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const queue = useReportQueue(page, PAGE_SIZE)

  return (
    <section className="grid gap-6">
      <PageTitle title={t('reports.title')} subtitle={t('reports.subtitle')} />

      {queue.isError ? (
        <ErrorMessage>{t('reports.loadFailed')}</ErrorMessage>
      ) : queue.isPending || !queue.data ? (
        <LoadingRows />
      ) : queue.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('reports.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {queue.data.items.map((item) => (
              <ReportCard key={`${item.targetType}-${item.targetId}`} item={item} />
            ))}
          </ul>
          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={queue.data.totalCount}
            onChange={(next) => setParams(next > 1 ? { page: String(next) } : {})}
          />
        </>
      )}
    </section>
  )
}

function ReportCard({ item }: { item: ReportedItem }) {
  const { t, i18n } = useTranslation('admin')
  const actions = useReportActions(item)
  const kind = item.targetType

  return (
    <li className="bg-card ring-border/60 grid gap-3 rounded-3xl p-4 ring-1">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="grid gap-1 text-sm">
          <span className="flex flex-wrap items-center gap-2">
            <Badge variant="outline">{t(`reports.target.${kind}`)}</Badge>
            <span className="font-medium">{t('reports.count', { count: item.reportCount })}</span>
            <span className="text-muted-foreground text-xs">
              {t('reports.latest', { when: new Date(item.lastReportedAt).toLocaleString(i18n.language) })}
            </span>
          </span>
          <Link to={`/products/${item.productId}`} className="text-muted-foreground hover:text-foreground text-xs hover:underline">
            {t('reports.on', { product: item.productName })}
          </Link>
        </div>
        <div className="flex flex-wrap gap-2">
          <TextPrompt
            mutation={actions.act}
            trigger={t(`reports.act.${kind}`)}
            title={t(`reports.act.${kind}`)}
            description={t('reports.actBody')}
            label={t('reports.reason')}
            submit={t(`reports.act.${kind}`)}
            onDone={() => toast.success(t('reports.acted'))}
            multiline
            maxLength={500}
            destructive
            outline
          />
          <Button
            size="sm"
            variant="outline"
            className="rounded-full px-3"
            disabled={actions.dismiss.isPending}
            onClick={() => actions.dismiss.mutateAsync().then(() => toast.success(t('reports.dismissed')), () => {})}
          >
            {t('reports.dismiss')}
          </Button>
        </div>
      </div>

      {item.excerpt && kind !== 'Product' && <p className="text-sm whitespace-pre-line">{item.excerpt}</p>}

      <div className="flex flex-wrap gap-1.5" aria-label={t('reports.reasons')}>
        {Object.entries(item.reasons).map(([reason, count]) => (
          <Badge key={reason} variant="secondary">
            {t(`reports.reasonName.${reason}`, { defaultValue: reason })} × {count}
          </Badge>
        ))}
      </div>
      {item.details.length > 0 && (
        <ul className="text-muted-foreground grid gap-1 text-xs">
          {item.details.map((d, i) => (
            <li key={i} className="whitespace-pre-line">“{d}”</li>
          ))}
        </ul>
      )}
      <ServerError error={actions.act.error ?? actions.dismiss.error} fallback={t('reports.loadFailed')} />
    </li>
  )
}
