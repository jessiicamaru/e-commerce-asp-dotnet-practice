import { useTranslation } from 'react-i18next'
import { XCircleIcon } from 'lucide-react'
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

/**
 * Cancel an order, after saying what that does (specs/039): the goods go back, what was paid is refunded,
 * and it cannot be undone. One component for the customer's order page and the staff one - the caller
 * hands in whose mutation it is. A refusal (a parcel started or sent meanwhile) is the server's words.
 */
export function CancelOrder({ cancel }: { cancel: UseMutationResult<unknown, Error, void> }) {
  const { t } = useTranslation('orders')

  return (
    <div className="grid justify-items-start gap-2">
      <AlertDialog>
        <AlertDialogTrigger
          render={<Button variant="outline" className="text-destructive h-9 rounded-full px-4" disabled={cancel.isPending} />}
        >
          <XCircleIcon /> {t('cancel.button')}
        </AlertDialogTrigger>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('cancel.title')}</AlertDialogTitle>
            <AlertDialogDescription>{t('cancel.body')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('cancel.keep')}</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => cancel.mutate(undefined, { onSuccess: () => toast.success(t('cancel.done')) })}
            >
              {t('cancel.confirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <ServerError error={cancel.error} fallback={t('order.loadFailed')} />
    </div>
  )
}
