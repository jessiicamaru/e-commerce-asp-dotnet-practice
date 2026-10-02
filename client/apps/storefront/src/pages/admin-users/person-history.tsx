import { useTranslation } from 'react-i18next'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { usePersonHistory } from '@ecommerce/core/hooks/accounts'
import { humanise } from '@/pages/admin-audit/format'

/**
 * What staff decided about a person before (specs/100): each decision, when, by whom and why, newest first. Drawn in
 * the lock and ban dialog, so a decision is never made as if it were the first, and on its own from the person's menu.
 * The server answers with Moderation entries only; a reason is shown as text.
 */
export function PersonHistory({ userId, pageSize, page = 1, onPage }: {
  userId: string
  pageSize: number
  page?: number
  /** Given: the full history, paged. Omitted: the latest few, and how many more there are. */
  onPage?: (page: number) => void
}) {
  const { t, i18n } = useTranslation('admin')
  const history = usePersonHistory(userId, page, pageSize)

  if (history.isError) return <ErrorMessage>{t('users.history.loadFailed')}</ErrorMessage>
  if (history.isPending || !history.data) return <LoadingRows />
  const { items, totalCount } = history.data
  if (totalCount === 0) return <p className="text-muted-foreground text-sm">{t('users.history.none')}</p>

  return (
    <div className="grid gap-2">
      <ol className="grid gap-2" aria-label={t('users.history.title')}>
        {items.map((e) => (
          <li key={e.id} className="bg-secondary/50 rounded-xl px-3 py-2 text-sm">
            <p className="flex flex-wrap items-baseline justify-between gap-x-3">
              <span className="font-medium">{t(`audit.action.${e.action}`, { defaultValue: humanise(e.action) })}</span>
              <time className="text-muted-foreground text-xs" dateTime={e.occurredAt}>
                {new Date(e.occurredAt).toLocaleString(i18n.language)}
              </time>
            </p>
            {e.reason && <p className="mt-0.5 whitespace-pre-line">“{e.reason}”</p>}
            {e.actorEmail && (
              <p className="text-muted-foreground text-xs">
                {t('users.history.by', { who: e.actorEmail, role: t(`roles.${e.actorRole}`, { defaultValue: e.actorRole ?? '' }) })}
              </p>
            )}
          </li>
        ))}
      </ol>
      {onPage ? (
        <Pager page={page} pageSize={pageSize} totalCount={totalCount} onChange={onPage} />
      ) : (
        totalCount > items.length && (
          <p className="text-muted-foreground text-xs">{t('users.history.more', { count: totalCount - items.length })}</p>
        )
      )}
    </div>
  )
}
