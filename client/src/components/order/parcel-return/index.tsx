import { useTranslation } from 'react-i18next'
import { SendIcon, ShieldQuestionIcon, Undo2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Price } from '@/components/shared/price'
import { ServerError } from '@/components/shared/server-error'
import { TextPrompt } from '@/components/shared/text-prompt'
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
import { useParcelReturn } from '@/hooks/order'
import type { ParcelReturn as ParcelReturnModel, Shipment } from '@/services/order/types'
import { canEscalate, canRequestReturn, canSendBack, returnDeadline } from '@/utils/order/returns'

/**
 * One parcel's return, as its buyer sees it (specs/067): the offer to return it while that is possible, then
 * where the return has got to and the one thing the buyer can do next - send it back once accepted, or take
 * a refusal to staff. Nothing at all for a parcel that cannot be returned and never was.
 */
export function ParcelReturn({ orderId, shipment, currency }: { orderId: string; shipment: Shipment; currency: string }) {
  const { t, i18n } = useTranslation('orders')
  const { request, escalate, sendBack } = useParcelReturn(orderId, shipment.id ?? '')
  const day = (at: string | Date) => new Date(at).toLocaleDateString(i18n.language)
  const ret = shipment.return

  if (!ret) {
    if (!canRequestReturn(shipment)) {
      return null
    }

    return (
      <div className="grid justify-items-start gap-1">
        <TextPrompt
          mutation={request}
          outline
          trigger={
            <>
              <Undo2Icon /> {t('return.button')}
            </>
          }
          title={t('return.title')}
          description={t('return.body')}
          label={t('return.reason')}
          submit={t('return.confirm')}
          multiline
          maxLength={1000}
          onDone={() => toast.success(t('return.requested'))}
        />
        <p className="text-muted-foreground text-xs">{t('return.until', { at: day(returnDeadline(shipment.deliveredAt!)) })}</p>
      </div>
    )
  }

  return (
    <div className="bg-muted/40 grid gap-2 rounded-xl p-3 text-sm" aria-label={t('return.heading')}>
      <p className="font-medium">{t('return.heading')}</p>
      <ReturnState ret={ret} currency={currency} day={day} />

      {canSendBack(ret) && (
        <TextPrompt
          mutation={sendBack}
          trigger={
            <>
              <SendIcon /> {t('return.sendBack')}
            </>
          }
          title={t('return.sendBackTitle')}
          description={t('return.sendBackBody')}
          label={t('return.tracking')}
          submit={t('return.sendBackConfirm')}
          maxLength={100}
          placeholder="VNPOST-…"
          onDone={() => toast.success(t('return.sentBackDone'))}
        />
      )}

      {canEscalate(ret) && (
        <div className="grid justify-items-start gap-1">
          <AlertDialog>
            <AlertDialogTrigger
              render={<Button variant="outline" className="h-9 rounded-full px-4" disabled={escalate.isPending} />}
            >
              <ShieldQuestionIcon /> {t('return.escalate')}
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>{t('return.escalateTitle')}</AlertDialogTitle>
                <AlertDialogDescription>{t('return.escalateBody')}</AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>{t('action.cancel', { ns: 'common' })}</AlertDialogCancel>
                <AlertDialogAction
                  onClick={() => escalate.mutate(undefined, { onSuccess: () => toast.success(t('return.escalated')) })}
                >
                  {t('return.escalateConfirm')}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
          <p className="text-muted-foreground text-xs">{t('return.escalateUntil', { at: day(returnDeadline(ret.decidedAt!)) })}</p>
          <ServerError error={escalate.error} fallback={t('order.loadFailed')} />
        </div>
      )}
    </div>
  )
}

/** Where the return has got to, in the buyer's words. */
function ReturnState({
  ret,
  currency,
  day,
}: {
  ret: ParcelReturnModel
  currency: string
  day: (at: string | Date) => string
}) {
  const { t } = useTranslation('orders')
  const who = ret.isShop ? t('return.theShop') : t('return.theSeller')

  switch (ret.status) {
    case 'Requested':
      return <p>{t('return.state.Requested', { at: day(ret.requestedAt), who })}</p>
    case 'Accepted':
      return (
        <p>
          {canSendBack(ret)
            ? t('return.state.Accepted', { at: day(returnDeadline(ret.decidedAt!)) })
            : t('return.state.AcceptedLate')}
        </p>
      )
    case 'Refused':
      return <p>{t('return.state.Refused', { who, reason: ret.decisionReason })}</p>
    case 'Escalated':
      return <p>{t('return.state.Escalated')}</p>
    case 'Rejected':
      return <p className="text-destructive">{t('return.state.Rejected', { reason: ret.decisionReason })}</p>
    case 'SentBack':
      return <p>{t('return.state.SentBack', { reference: ret.trackingReference })}</p>
    case 'Received':
      return (
        <p className="text-emerald-700 dark:text-emerald-400">
          {t('return.state.Received')}{' '}
          {ret.refundAmount !== null && <Price value={ret.refundAmount} currency={currency} className="font-semibold" />}
        </p>
      )
    default:
      return <p>{ret.status}</p>
  }
}
