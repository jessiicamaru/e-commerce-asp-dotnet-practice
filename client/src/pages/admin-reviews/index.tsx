import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { StarRating } from '@/components/product/star-rating'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { PAGE_SIZE } from '@/constants/shared'
import { useReviewModeration, useStaffReviews } from '@/hooks/review'
import type { Review } from '@/services/review/types'
import { cn } from '@/utils/shared'

/**
 * What buyers wrote, for moderators (specs/046). Hiding takes a review off its product page and out of
 * the average - the server recomputes both - and asks why; showing it again puts it back in both.
 */
export function AdminReviewsPage() {
  const { t, i18n } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const hidden = params.get('tab') === 'hidden'
  const page = Number(params.get('page') ?? '1') || 1
  const [hiding, setHiding] = useState<Review | null>(null)

  const list = useStaffReviews(hidden, page, PAGE_SIZE)
  const moderate = useReviewModeration()

  const go = (next: { hidden?: boolean; page?: number }) => {
    const merged = new URLSearchParams()
    if (next.hidden ?? hidden) merged.set('tab', 'hidden')
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }

  return (
    <section className="grid gap-6">
      <PageTitle title={t('reviews.title')} subtitle={t('reviews.subtitle')} />

      <div role="tablist" className="bg-card ring-border/60 flex gap-1 justify-self-start rounded-3xl p-1 ring-1">
        {[false, true].map((tab) => (
          <button
            key={String(tab)}
            type="button"
            role="tab"
            aria-selected={tab === hidden}
            onClick={() => go({ hidden: tab, page: 1 })}
            className={cn(
              'rounded-full px-3 py-1.5 text-sm font-medium transition-colors',
              tab === hidden ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(tab ? 'reviews.tab.hidden' : 'reviews.tab.visible')}
          </button>
        ))}
      </div>

      <ServerError error={moderate.hide.error ?? moderate.restore.error} fallback={t('reviews.loadFailed')} />

      {list.isError ? (
        <ErrorMessage>{t('reviews.loadFailed')}</ErrorMessage>
      ) : list.isPending || !list.data ? (
        <LoadingRows />
      ) : list.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('reviews.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {list.data.items.map((r) => (
              <li key={r.id} className="bg-card ring-border/60 grid gap-2 rounded-3xl p-4 ring-1">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="grid gap-1 text-sm">
                    <span className="flex flex-wrap items-center gap-2">
                      <StarRating value={r.rating} label={String(r.rating)} />
                      <span className="font-medium">{r.authorName}</span>
                      <span className="text-muted-foreground text-xs">{new Date(r.createdAt).toLocaleString(i18n.language)}</span>
                    </span>
                    <Link to={`/products/${r.productId}`} className="text-muted-foreground hover:text-foreground text-xs hover:underline">
                      {t('reviews.on', { product: r.productName })}
                    </Link>
                  </div>
                  {hidden ? (
                    <Button
                      size="sm"
                      variant="outline"
                      className="rounded-full px-3"
                      onClick={() => moderate.restore.mutate(r.id, { onSuccess: () => toast.success(t('reviews.restored')) })}
                    >
                      {t('reviews.restore')}
                    </Button>
                  ) : (
                    <Button size="sm" variant="outline" className="rounded-full px-3" onClick={() => setHiding(r)}>
                      {t('reviews.hide')}
                    </Button>
                  )}
                </div>
                {r.body && <p className="text-sm whitespace-pre-line">{r.body}</p>}
                {r.hiddenReason && <p className="text-destructive text-xs">{t('reviews.hiddenBecause', { reason: r.hiddenReason })}</p>}
              </li>
            ))}
          </ul>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onChange={(next) => go({ page: next })} />
        </>
      )}

      <Dialog open={hiding !== null} onOpenChange={(open) => !open && setHiding(null)}>
        {hiding && (
          <HideForm
            key={hiding.id}
            onConfirm={(reason) => {
              moderate.hide.mutate({ id: hiding.id, reason }, { onSuccess: () => toast.success(t('reviews.hidden')) })
              setHiding(null)
            }}
          />
        )}
      </Dialog>
    </section>
  )
}

function HideForm({ onConfirm }: { onConfirm: (reason: string) => void }) {
  const { t } = useTranslation('admin')
  const [reason, setReason] = useState('')

  return (
    <DialogContent>
      <form
        className="grid gap-4"
        onSubmit={(event) => {
          event.preventDefault()
          if (reason.trim()) onConfirm(reason.trim())
        }}
      >
        <DialogHeader>
          <DialogTitle>{t('reviews.hideTitle')}</DialogTitle>
          <DialogDescription>{t('reviews.hideBody')}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-2">
          <Label htmlFor="hide-reason">{t('reviews.reason')}</Label>
          <Textarea id="hide-reason" value={reason} maxLength={500} onChange={(event) => setReason(event.target.value)} />
        </div>
        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>{t('action.cancel', { ns: 'common' })}</DialogClose>
          <Button type="submit" variant="destructive" disabled={!reason.trim()}>
            {t('reviews.confirmHide')}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  )
}
