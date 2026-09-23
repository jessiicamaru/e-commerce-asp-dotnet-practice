import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { CheckIcon, ExternalLinkIcon } from 'lucide-react'
import { ProductImage } from '@/components/product/product-image'
import { PageTitle } from '@/components/seller/page-title'
import { Price } from '@/components/shared/price'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Button, buttonVariants } from '@/components/ui/button'
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
import { useReviewDecision, useReviewQueue } from '@/hooks/moderation'
import type { Product, ReviewStatus } from '@/services/product/types'
import { cn } from '@/utils/shared'

const TABS: ReviewStatus[] = ['Pending', 'Rejected', 'Approved']

type Refusing = { kind: 'reject' | 'takeDown'; product: Product }

/**
 * What sellers want on the shelf (specs/045). A waiting product is approved at a press or rejected with a
 * reason; one on sale can be taken down, with a reason. The seller reads the reason, so there is no
 * refusing without one - and two staff deciding the same product is the server's 409, in its words.
 */
export function AdminProductsPage() {
  const { t } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const asked = params.get('status') as ReviewStatus | null
  const status = asked && TABS.includes(asked) ? asked : 'Pending'
  const page = Number(params.get('page') ?? '1') || 1
  const [refusing, setRefusing] = useState<Refusing | null>(null)

  const queue = useReviewQueue(status, page, PAGE_SIZE)
  const decide = useReviewDecision()
  const failed = decide.approve.error ?? decide.reject.error ?? decide.takeDown.error

  const go = (next: { status?: ReviewStatus; page?: number }) => {
    const merged = new URLSearchParams()
    merged.set('status', next.status ?? status)
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }

  return (
    <section className="grid gap-6">
      <PageTitle title={t('review.title')} subtitle={t('review.subtitle')} />

      <div role="tablist" className="bg-card ring-border/60 flex flex-wrap gap-1 justify-self-start rounded-3xl p-1 ring-1">
        {TABS.map((tab) => (
          <button
            key={tab}
            type="button"
            role="tab"
            aria-selected={tab === status}
            onClick={() => go({ status: tab })}
            className={cn(
              'rounded-full px-3 py-1.5 text-sm font-medium transition-colors',
              tab === status ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(`review.tab.${tab}`)}
          </button>
        ))}
      </div>

      <ServerError error={failed} fallback={t('review.loadFailed')} />

      {queue.isError ? (
        <ErrorMessage>{t('review.loadFailed')}</ErrorMessage>
      ) : queue.isPending || !queue.data ? (
        <LoadingRows />
      ) : queue.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('review.none')}</p>
      ) : (
        <>
          <div className="grid gap-3">
            {queue.data.items.map((p) => (
              <article key={p.id} className="bg-card ring-border/60 grid gap-3 rounded-3xl p-4 ring-1 sm:grid-cols-[6rem_1fr]">
                <span className="w-24">
                  <ProductImage product={p} thumb />
                </span>
                <div className="grid min-w-0 gap-2">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <h3 className="font-semibold">{p.name}</h3>
                      <p className="text-muted-foreground text-sm">
                        {t('review.soldBy', { shop: p.sellerName ?? t('review.theShop') })} ·{' '}
                        <Price value={p.price} currency={p.currency} />
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Link
                        to={`/products/${p.id}`}
                        className={cn(buttonVariants({ variant: 'ghost', size: 'sm' }), 'rounded-full px-3')}
                      >
                        <ExternalLinkIcon /> {t('review.view')}
                      </Link>
                      {status === 'Pending' && (
                        <>
                          <Button
                            size="sm"
                            className="rounded-full px-3"
                            disabled={decide.approve.isPending}
                            onClick={() =>
                              decide.approve.mutate(p.id, { onSuccess: () => toast.success(t('review.approved', { name: p.name })) })
                            }
                          >
                            <CheckIcon /> {t('review.approve')}
                          </Button>
                          <Button size="sm" variant="outline" className="rounded-full px-3" onClick={() => setRefusing({ kind: 'reject', product: p })}>
                            {t('review.reject')}
                          </Button>
                        </>
                      )}
                      {status === 'Approved' && (
                        <Button size="sm" variant="outline" className="rounded-full px-3" onClick={() => setRefusing({ kind: 'takeDown', product: p })}>
                          {t('review.takeDown')}
                        </Button>
                      )}
                    </div>
                  </div>
                  {p.description && <p className="line-clamp-3 text-sm">{p.description}</p>}
                  {p.reviewReason && <p className="text-destructive text-sm">{p.reviewReason}</p>}
                </div>
              </article>
            ))}
          </div>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={queue.data.totalCount} onChange={(next) => go({ page: next })} />
        </>
      )}

      <Dialog open={refusing !== null} onOpenChange={(open) => !open && setRefusing(null)}>
        {refusing && (
          <ReasonForm
            key={`${refusing.kind}-${refusing.product.id}`}
            refusing={refusing}
            onConfirm={(reason) => {
              const { kind, product } = refusing
              const mutation = kind === 'reject' ? decide.reject : decide.takeDown
              mutation.mutate(
                { id: product.id, reason },
                { onSuccess: () => toast.success(t(kind === 'reject' ? 'review.rejected' : 'review.takenDown', { name: product.name })) },
              )
              setRefusing(null)
            }}
          />
        )}
      </Dialog>
    </section>
  )
}

function ReasonForm({ refusing, onConfirm }: { refusing: Refusing; onConfirm: (reason: string) => void }) {
  const { t } = useTranslation('admin')
  const [reason, setReason] = useState('')
  const reject = refusing.kind === 'reject'

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
          <DialogTitle>{t(reject ? 'review.rejectTitle' : 'review.takeDownTitle', { name: refusing.product.name })}</DialogTitle>
          <DialogDescription>{t('review.reasonBody')}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-2">
          <Label htmlFor="review-reason">{t('review.reason')}</Label>
          <Textarea id="review-reason" value={reason} maxLength={500} onChange={(event) => setReason(event.target.value)} />
        </div>
        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>{t('action.cancel', { ns: 'common' })}</DialogClose>
          <Button type="submit" variant="destructive" disabled={!reason.trim()}>
            {t(reject ? 'review.confirmReject' : 'review.confirmTakeDown')}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  )
}
