import { useTranslation } from 'react-i18next'
import { cn } from '@/utils/shared'

/** A colour per state, so a list is read at a glance: waiting amber, under way blue, sent green, stopped red. */
const TONE: Record<string, string> = {
  Submitted: 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200',
  Paid: 'bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-200',
  Preparing: 'bg-violet-100 text-violet-900 dark:bg-violet-950 dark:text-violet-200',
  Shipped: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200',
  Failed: 'bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-200',
  Cancelled: 'bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-200',
}

/**
 * An order's (or a part's) state as a short coloured word (specs/132, #248) - "Paid", "Shipped", "1 of 2 shipped" -
 * where a list used to print a grey sentence. The sentence stays on the order's own page.
 */
export function OrderStatusChip({
  status,
  shipped,
  parcels,
  className,
}: {
  status: string
  /** Parcels sent and in all (specs/035): "1 of 2 shipped" while some have gone and some have not. */
  shipped?: number
  parcels?: number
  className?: string
}) {
  const { t } = useTranslation('orders')
  const partly = !!parcels && parcels > 1 && !!shipped && shipped < parcels && status !== 'Cancelled'
  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-xs font-medium',
        TONE[status] ?? 'bg-secondary',
        className,
      )}
    >
      {partly ? t('chip.partly', { shipped, count: parcels }) : t(`chip.${status}`, { defaultValue: status })}
    </span>
  )
}
