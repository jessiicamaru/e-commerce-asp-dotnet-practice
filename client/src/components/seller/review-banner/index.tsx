import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { useResubmitProduct } from '@/hooks/product'
import type { Product } from '@/services/product/types'

/**
 * What the moderators said about this listing (specs/045) - waiting, or rejected with why and a way to
 * send it back. An approved one says only that changing its words or photographs sends it back, which is
 * the one thing a seller would not guess.
 */
export function ReviewBanner({ product }: { product: Product }) {
  const { t } = useTranslation('seller')
  const resubmit = useResubmitProduct(product.id)

  if (product.reviewStatus === 'Approved') {
    return <p className="text-muted-foreground text-sm">{t('review.approvedHint')}</p>
  }

  const rejected = product.reviewStatus === 'Rejected'
  return (
    <div
      role="status"
      className={rejected ? 'bg-destructive/10 grid gap-2 rounded-3xl p-4 text-sm' : 'bg-secondary grid gap-2 rounded-3xl p-4 text-sm'}
    >
      <p className="font-semibold">{t(`review.title.${product.reviewStatus}`)}</p>
      <p>{rejected ? t('review.rejectedBody', { reason: product.reviewReason }) : t('review.pendingBody')}</p>
      {rejected && (
        <Button
          size="sm"
          className="justify-self-start rounded-full"
          disabled={resubmit.isPending}
          onClick={() => resubmit.mutate(undefined, { onSuccess: () => toast.success(t('review.resubmitted')) })}
        >
          {t('review.resubmit')}
        </Button>
      )}
      <ServerError error={resubmit.error} fallback={t('review.failed')} />
    </div>
  )
}
