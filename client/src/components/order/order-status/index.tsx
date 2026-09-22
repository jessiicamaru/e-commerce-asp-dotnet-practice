import { useTranslation } from 'react-i18next'
import { Loader2 } from 'lucide-react'
import { cn } from '@/utils/shared'
import { describeOrderStatus, orderStatusTone } from '@/utils/order'

const TONES = {
  good: 'text-emerald-700 dark:text-emerald-400',
  bad: 'text-destructive',
  waiting: 'text-muted-foreground',
}

/** What is happening to an order, in a sentence. `waiting` is the saga still settling it. */
export function OrderStatus({
  status,
  failureReason,
  showSpinner = false,
  overrideMessage,
}: {
  status: string
  failureReason: string | null
  showSpinner?: boolean
  overrideMessage?: string
}) {
  const { t } = useTranslation('orders')

  return (
    <p role="status" className={cn('bg-muted flex items-center gap-2 rounded-md px-3 py-2', TONES[orderStatusTone(status)])}>
      {showSpinner && <Loader2 className="size-4 animate-spin" aria-hidden="true" />}
      {overrideMessage ?? describeOrderStatus(t, status, failureReason)}
    </p>
  )
}
