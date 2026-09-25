import type { ReactNode } from 'react'
import type { UseMutationResult } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckIcon, PackageOpenIcon, XIcon } from 'lucide-react'
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
import type { ParcelReturn } from '@/services/order/types'
import type { ReturnStep } from '@/utils/order/returns'

/**
 * A parcel's return, for whoever answers it (specs/067): the buyer's reason, where it has got to, and the one
 * step that is theirs - accept or refuse a request, or say the parcel came back. A seller's parcel on the
 * sale page, the shop's parcel or an escalated one in the administrator's console: the steps and the
 * confirmations are the same, so there is one component and the caller hands in whose mutations these are
 * (the same bargain as `ParcelActions`, specs/038).
 */
export function ReturnDecision({
  ret,
  currency,
  step,
  final = false,
  accept,
  refuse,
  receive,
}: {
  ret: ParcelReturn
  /** The order's own, frozen at checkout - what the refund is in. */
  currency: string
  /** What the caller may do now; null draws the state and nothing to press. */
  step: ReturnStep
  /** Staff's word on an escalated return: a refusal is then a rejection for good. */
  final?: boolean
  accept: UseMutationResult<unknown, Error, void>
  refuse: UseMutationResult<unknown, Error, string>
  receive: UseMutationResult<unknown, Error, void>
}) {
  const { t, i18n } = useTranslation('seller')
  const day = (at: string | null) => (at ? new Date(at).toLocaleDateString(i18n.language) : '')

  return (
    <div className="grid gap-3 text-sm">
      <div className="grid gap-1">
        <p className="text-muted-foreground text-xs">{t('return.reason')}</p>
        <blockquote className="border-border border-l-2 pl-3 italic">{ret.reason}</blockquote>
      </div>

      <p className={ret.status === 'Rejected' ? 'text-destructive' : undefined}>
        {t(`return.state.${ret.status}`, {
          at: day(ret.status === 'Requested' ? ret.requestedAt : ret.status === 'SentBack' ? ret.sentBackAt : ret.receivedAt),
          reason: ret.decisionReason,
          reference: ret.trackingReference,
          defaultValue: ret.status,
        })}{' '}
        {ret.status === 'Received' && ret.refundAmount !== null && (
          <Price value={ret.refundAmount} currency={currency} className="font-semibold" />
        )}
      </p>

      {step === 'decide' && (
        <div className="flex flex-wrap gap-2">
          <Confirm
            mutation={accept}
            trigger={
              <>
                <CheckIcon /> {t('return.accept')}
              </>
            }
            title={t('return.acceptTitle')}
            body={t('return.acceptBody')}
            confirm={t('return.accept')}
            done={t('return.accepted')}
          />
          <TextPrompt
            mutation={refuse}
            outline
            destructive
            trigger={
              <>
                <XIcon /> {t(final ? 'return.reject' : 'return.refuse')}
              </>
            }
            title={t(final ? 'return.rejectTitle' : 'return.refuseTitle')}
            description={t(final ? 'return.rejectBody' : 'return.refuseBody')}
            label={t('return.refuseReason')}
            submit={t(final ? 'return.reject' : 'return.refuse')}
            multiline
            maxLength={500}
            onDone={() => toast.success(t('return.refused'))}
          />
        </div>
      )}

      {step === 'receive' && (
        <Confirm
          mutation={receive}
          trigger={
            <>
              <PackageOpenIcon /> {t('return.receive')}
            </>
          }
          title={t('return.receiveTitle')}
          body={t('return.receiveBody')}
          confirm={t('return.receiveConfirm')}
          done={t('return.receivedDone')}
        />
      )}

      <ServerError error={accept.error ?? receive.error} fallback={t('listing.loadFailed')} />
    </div>
  )
}

/** A step that cannot be taken back, asked about first. */
function Confirm({
  mutation,
  trigger,
  title,
  body,
  confirm,
  done,
}: {
  mutation: UseMutationResult<unknown, Error, void>
  trigger: ReactNode
  title: string
  body: string
  confirm: string
  done: string
}) {
  const { t } = useTranslation('common')

  return (
    <AlertDialog>
      <AlertDialogTrigger render={<Button className="h-9 rounded-full px-4" disabled={mutation.isPending} />}>
        {trigger}
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{body}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{t('action.cancel')}</AlertDialogCancel>
          <AlertDialogAction onClick={() => mutation.mutate(undefined, { onSuccess: () => toast.success(done) })}>
            {confirm}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
