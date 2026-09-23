import { useTranslation } from 'react-i18next'
import { PackageCheckIcon } from 'lucide-react'
import { toast } from 'sonner'
import type { UseMutationResult } from '@tanstack/react-query'
import { ServerError } from '@/components/shared/server-error'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import type { Shipment } from '@/services/order/types'
import { canReceive } from '@/utils/order/delivery'

/**
 * "I've received it" for one parcel, or when it was received (specs/040).
 *
 * <p>
 * Confirmed first, because it is what releases the seller's money for that parcel and cannot be taken
 * back. A parcel nobody confirms is taken as received a week after it shipped - which the page says, so a
 * customer knows not saying anything is also an answer.
 * </p>
 */
export function ReceiveParcel({
  shipment,
  receive,
}: {
  shipment: Shipment
  receive: UseMutationResult<unknown, Error, string>
}) {
  const { t, i18n } = useTranslation('orders')

  if (shipment.deliveredAt) {
    return (
      <p className="text-xs text-emerald-700 dark:text-emerald-400">
        {t(shipment.deliveryConfirmedBy === 'Auto' ? 'received.auto' : 'received.at', {
          at: new Date(shipment.deliveredAt).toLocaleDateString(i18n.language),
        })}
      </p>
    )
  }

  if (!canReceive(shipment)) {
    return null
  }

  return (
    <div className="grid justify-items-start gap-1">
      <AlertDialog>
        <AlertDialogTrigger render={<Button size="sm" className="rounded-full px-3" disabled={receive.isPending} />}>
          <PackageCheckIcon /> {t('received.button')}
        </AlertDialogTrigger>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('received.title')}</AlertDialogTitle>
            <AlertDialogDescription>{t('received.body')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('action.cancel', { ns: 'common' })}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => receive.mutate(shipment.id!, { onSuccess: () => toast.success(t('received.done')) })}
            >
              {t('received.confirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <p className="text-muted-foreground text-xs">{t('received.hint')}</p>
      <ServerError error={receive.error} fallback={t('order.loadFailed')} />
    </div>
  )
}
