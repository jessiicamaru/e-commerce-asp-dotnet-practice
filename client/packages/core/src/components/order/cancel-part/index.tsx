import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { XCircleIcon } from 'lucide-react'
import { toast } from 'sonner'
import type { UseMutationResult } from '@tanstack/react-query'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { Button } from '@ecommerce/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@ecommerce/ui/dialog'
import { Label } from '@ecommerce/ui/label'
import { Textarea } from '@ecommerce/ui/textarea'

/**
 * Cancel ONE part of an order before it ships, with a reason the buyer reads (specs/104) - a seller's own, or staff's
 * for the shop's. The rest of the order goes on; the last part cancels it. The caller hands in whose mutation it is,
 * and a refusal (shipped meanwhile) is the server's words. `mutateAsync().then`, so the toast survives the dialog
 * closing (specs/080).
 */
export function CancelPart({ cancel }: { cancel: UseMutationResult<unknown, Error, string> }) {
  const { t } = useTranslation('orders')
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')

  return (
    <div className="grid justify-items-start gap-2">
      <Dialog
        open={open}
        onOpenChange={(next) => {
          setOpen(next)
          if (next) {
            setReason('')
            cancel.reset()
          }
        }}
      >
        <DialogTrigger render={<Button variant="outline" className="text-destructive h-9 rounded-full px-4" disabled={cancel.isPending} />}>
          <XCircleIcon /> {t('cancelPart.button')}
        </DialogTrigger>
        <DialogContent className="sm:max-w-md">
          <form
            className="grid gap-4"
            onSubmit={(event) => {
              event.preventDefault()
              if (!reason.trim()) return
              cancel.mutateAsync(reason.trim()).then(() => {
                toast.success(t('cancelPart.done'))
                setOpen(false)
              }, () => {})
            }}
          >
            <DialogHeader>
              <DialogTitle>{t('cancelPart.title')}</DialogTitle>
              <DialogDescription>{t('cancelPart.body')}</DialogDescription>
            </DialogHeader>
            <div className="grid gap-2">
              <Label htmlFor="cancel-part-reason">{t('cancelPart.reason')}</Label>
              <Textarea id="cancel-part-reason" value={reason} maxLength={500} onChange={(event) => setReason(event.target.value)} />
            </div>
            <ServerError error={cancel.error} fallback={t('order.loadFailed')} />
            <DialogFooter>
              <DialogClose render={<Button type="button" variant="ghost" />}>{t('cancelPart.keep')}</DialogClose>
              <Button type="submit" variant="destructive" disabled={!reason.trim() || cancel.isPending}>
                {t('cancelPart.confirm')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
