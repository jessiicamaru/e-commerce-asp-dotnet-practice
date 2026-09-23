import { useTranslation } from 'react-i18next'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { CheckCheckIcon } from 'lucide-react'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { useMarkRead, useNotifications, useUnreadCount } from '@/hooks/notifications'
import { PAGE_SIZE } from '@/constants/shared'
import { describeNotification } from '@/utils/notifications'
import { cn } from '@/utils/shared'

/** Every notification the reader has had (specs/042), newest first, all or unread only. */
export function NotificationsPage() {
  const { t, i18n } = useTranslation('notifications')
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const unreadOnly = params.get('unread') === '1'
  const page = Number(params.get('page') ?? '1') || 1
  const list = useNotifications(page, PAGE_SIZE, unreadOnly)
  const unread = useUnreadCount(true).data ?? 0
  const mark = useMarkRead()

  return (
    <section className="grid gap-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-bold tracking-tight">{t('title')}</h1>
        <Button variant="outline" className="rounded-full" disabled={unread === 0 || mark.all.isPending} onClick={() => mark.all.mutate()}>
          <CheckCheckIcon /> {t('markAll')}
        </Button>
      </header>

      <div role="tablist" className="bg-card ring-border/60 flex gap-1 justify-self-start rounded-full p-1 ring-1">
        {[false, true].map((only) => (
          <button
            key={String(only)}
            type="button"
            role="tab"
            aria-selected={only === unreadOnly}
            onClick={() => setParams(only ? { unread: '1' } : {})}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              only === unreadOnly ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {only ? t('unreadTab', { count: unread }) : t('allTab')}
          </button>
        ))}
      </div>

      {list.isError ? (
        <ErrorMessage>{t('loadFailed')}</ErrorMessage>
      ) : list.isPending || !list.data ? (
        <LoadingRows />
      ) : list.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('none')}</p>
      ) : (
        <>
          <ul className="grid gap-2">
            {list.data.items.map((n) => (
              <li key={n.id}>
                <button
                  type="button"
                  onClick={() => {
                    if (!n.readAt) mark.one.mutate(n.id)
                    if (n.link) navigate(n.link)
                  }}
                  className={cn(
                    'bg-card ring-border/60 hover:ring-primary/60 grid w-full gap-1 rounded-3xl p-4 text-left ring-1 transition-all',
                    !n.readAt && 'ring-primary/40',
                  )}
                >
                  <span className={cn('text-sm', !n.readAt && 'font-semibold')}>
                    {!n.readAt && <span aria-hidden className="bg-primary mr-2 inline-block size-2 rounded-full" />}
                    {describeNotification(t, n)}
                  </span>
                  <span className="text-muted-foreground text-xs">{new Date(n.createdAt).toLocaleString(i18n.language)}</span>
                </button>
              </li>
            ))}
          </ul>
          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={list.data.totalCount}
            onChange={(next) => setParams(unreadOnly ? { unread: '1', page: String(next) } : { page: String(next) })}
          />
        </>
      )}
    </section>
  )
}
