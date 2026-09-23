import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import type { ReviewStatus } from '@/services/product/types'

/**
 * Where a listing stands with the moderators (specs/045), for its seller and staff. Nothing for an
 * approved one - on sale is what a listing is expected to be.
 */
export function ReviewBadge({ status }: { status: ReviewStatus }) {
  const { t } = useTranslation('seller')

  if (status === 'Approved') return null
  return <Badge variant={status === 'Rejected' ? 'destructive' : 'secondary'}>{t(`review.status.${status}`)}</Badge>
}
