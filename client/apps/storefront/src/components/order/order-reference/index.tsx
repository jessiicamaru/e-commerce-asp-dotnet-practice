import { useTranslation } from 'react-i18next'
import { CopyIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@ecommerce/ui/button'
import { orderReference } from '@ecommerce/core/utils/order'

/**
 * An order's short reference with a button to copy it (specs/132, #248) - the eight characters the notices, the lists
 * and staff search all use, so a customer quoting it and staff finding it mean the same order. The page showed the
 * 36-character id before.
 */
export function OrderReference({ orderId }: { orderId: string }) {
  const { t } = useTranslation('orders')
  const reference = orderReference(orderId)

  const copy = () =>
    navigator.clipboard
      ?.writeText(reference)
      .then(() => toast.success(t('copied')))
      .catch(() => {})

  return (
    <span className="inline-flex items-center gap-1">
      <span className="font-mono text-sm font-semibold">{t('reference', { reference })}</span>
      <Button type="button" variant="ghost" size="icon-sm" className="rounded-full" aria-label={t('copy')} onClick={copy}>
        <CopyIcon />
      </Button>
    </span>
  )
}
