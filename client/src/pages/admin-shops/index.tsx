import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { CheckIcon } from 'lucide-react'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@/components/ui/badge'
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
import { useDecideShopApplication, useShopApplications } from '@/hooks/shop-applications'
import type { ShopApplication, ShopApplicationStatus } from '@/services/shop-applications/types'
import { cn } from '@/utils/shared'

const TABS: ShopApplicationStatus[] = ['Pending', 'Approved', 'Rejected']

/**
 * Who is asking to sell (specs/044) - a moderator's first queue. Approving opens the shop at once;
 * rejecting needs a reason, because the applicant reads it. Two staff deciding the same application is
 * the server's 409, shown in its words.
 */
export function AdminShopsPage() {
  const { t, i18n } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const asked = params.get('status') as ShopApplicationStatus | null
  const status = asked && TABS.includes(asked) ? asked : 'Pending'
  const page = Number(params.get('page') ?? '1') || 1
  const [rejecting, setRejecting] = useState<ShopApplication | null>(null)

  const list = useShopApplications(status, page, PAGE_SIZE)
  const decide = useDecideShopApplication()
  const failed = decide.approve.error ?? decide.reject.error

  const go = (next: { status?: ShopApplicationStatus; page?: number }) => {
    const merged = new URLSearchParams()
    merged.set('status', next.status ?? status)
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }

  return (
    <section className="grid gap-6">
      <PageTitle title={t('shops.title')} subtitle={t('shops.subtitle')} />

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
            {t(`shops.tab.${tab}`)}
          </button>
        ))}
      </div>

      <ServerError error={failed} fallback={t('shops.loadFailed')} />

      {list.isError ? (
        <ErrorMessage>{t('shops.loadFailed')}</ErrorMessage>
      ) : list.isPending || !list.data ? (
        <LoadingRows />
      ) : list.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('shops.none')}</p>
      ) : (
        <>
          <div className="grid gap-3">
            {list.data.items.map((a) => (
              <article key={a.id} className="bg-card ring-border/60 grid gap-2 rounded-3xl p-5 ring-1">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h3 className="font-semibold">{a.shopName}</h3>
                    <p className="text-muted-foreground text-sm break-all">
                      {a.applicantName} · {a.applicantEmail}
                      {a.status === 'Pending' && a.applicantEmailConfirmed === false && (
                        <Badge variant="outline" className="ml-2">{t('shops.unconfirmed')}</Badge>
                      )}
                    </p>
                  </div>
                  {a.status === 'Pending' && (
                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        className="rounded-full px-3"
                        disabled={decide.approve.isPending}
                        onClick={() =>
                          decide.approve.mutate(a.id, { onSuccess: () => toast.success(t('shops.approved', { shop: a.shopName })) })
                        }
                      >
                        <CheckIcon /> {t('shops.approve')}
                      </Button>
                      <Button size="sm" variant="outline" className="rounded-full px-3" onClick={() => setRejecting(a)}>
                        {t('shops.reject')}
                      </Button>
                    </div>
                  )}
                </div>
                <p className="text-sm whitespace-pre-line">{a.description ?? t('shops.noDescription')}</p>
                <p className="text-muted-foreground text-xs">
                  {t('shops.applied', { when: new Date(a.createdAt).toLocaleString(i18n.language) })}
                  {a.phone && <> · {t('shops.phone', { phone: a.phone })}</>}
                  {a.decidedAt && <> · {t('shops.decided', { when: new Date(a.decidedAt).toLocaleString(i18n.language) })}</>}
                </p>
                {a.decisionReason && <p className="text-sm">{a.decisionReason}</p>}
              </article>
            ))}
          </div>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={list.data.totalCount} onChange={(next) => go({ page: next })} />
        </>
      )}

      <Dialog open={rejecting !== null} onOpenChange={(open) => !open && setRejecting(null)}>
        {rejecting && (
          <RejectForm
            key={rejecting.id}
            application={rejecting}
            onConfirm={(reason) => {
              decide.reject.mutate(
                { id: rejecting.id, reason },
                { onSuccess: () => toast.success(t('shops.rejected', { shop: rejecting.shopName })) },
              )
              setRejecting(null)
            }}
          />
        )}
      </Dialog>
    </section>
  )
}

function RejectForm({ application, onConfirm }: { application: ShopApplication; onConfirm: (reason: string) => void }) {
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
          <DialogTitle>{t('shops.rejectTitle', { shop: application.shopName })}</DialogTitle>
          <DialogDescription>{t('shops.rejectBody')}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-2">
          <Label htmlFor="reject-reason">{t('shops.reason')}</Label>
          <Textarea id="reject-reason" value={reason} maxLength={500} onChange={(event) => setReason(event.target.value)} />
        </div>
        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>{t('action.cancel', { ns: 'common' })}</DialogClose>
          <Button type="submit" variant="destructive" disabled={!reason.trim()}>
            {t('shops.confirmReject')}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  )
}
