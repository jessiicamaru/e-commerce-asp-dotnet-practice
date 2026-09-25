import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { PAGE_SIZE } from '@/constants/shared'
import { useReturnQueue } from '@/hooks/admin'
import { RETURN_QUEUE_STATES, type ReturnQueueState } from '@/services/admin/types'
import { cn } from '@/utils/shared'

/**
 * Returns, one state at a time (specs/067), oldest waiting first. It opens on the escalated ones - the
 * disputes only staff can settle - then what the shop itself must answer or receive. Each row leads to its
 * order, where the steps are.
 *
 * <p>
 * No amounts here: a return carries no currency of its own, and a number without one is what specs/022
 * forbids. The order page has it, in the order's currency.
 * </p>
 */
export function AdminReturnsPage() {
  const { t, i18n } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const asked = params.get('status')
  const status: ReturnQueueState = RETURN_QUEUE_STATES.includes(asked as ReturnQueueState) ? (asked as ReturnQueueState) : 'Escalated'
  const page = Number(params.get('page') ?? '1') || 1
  const queue = useReturnQueue(status, page, PAGE_SIZE)

  return (
    <section className="grid gap-6">
      <PageTitle title={t('returns.title')} subtitle={t('returns.subtitle')} />

      <div role="tablist" className="bg-card ring-border/60 flex flex-wrap gap-1 justify-self-start rounded-3xl p-1 ring-1">
        {RETURN_QUEUE_STATES.map((state) => (
          <button
            key={state}
            type="button"
            role="tab"
            aria-selected={state === status}
            onClick={() => setParams({ status: state })}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              state === status ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(`returns.state.${state}`)}
          </button>
        ))}
      </div>

      {queue.isError ? (
        <ErrorMessage>{t('returns.loadFailed')}</ErrorMessage>
      ) : queue.isPending || !queue.data ? (
        <LoadingRows />
      ) : queue.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('returns.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {queue.data.items.map((ret) => (
              <li key={ret.id}>
                <Link
                  to={`/admin/orders/${ret.orderId}`}
                  className="bg-card ring-border/60 hover:ring-primary/60 grid gap-1 rounded-3xl p-4 ring-1 transition-all"
                >
                  <span className="flex flex-wrap items-center gap-2 font-medium">
                    {t('returns.requestedAt', { at: new Date(ret.requestedAt).toLocaleString(i18n.language) })}
                    <Badge variant="outline">{ret.isShop ? t('returns.theShop') : t('returns.aSeller')}</Badge>
                  </span>
                  <span className="line-clamp-2 text-sm">{ret.reason}</span>
                  {ret.decisionReason && (
                    <span className="text-muted-foreground text-xs">{t('returns.refusedBecause', { reason: ret.decisionReason })}</span>
                  )}
                </Link>
              </li>
            ))}
          </ul>
          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={queue.data.totalCount}
            onChange={(next) => setParams({ status, page: String(next) })}
          />
        </>
      )}
    </section>
  )
}
