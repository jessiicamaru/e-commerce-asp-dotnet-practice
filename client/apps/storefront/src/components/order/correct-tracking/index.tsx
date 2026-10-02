import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PencilIcon } from 'lucide-react'
import { toast } from 'sonner'
import type { UseMutationResult } from '@tanstack/react-query'
import { ServerError } from '@/components/shared/server-error'
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
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'

/**
 * Correct a mistyped tracking reference while the parcel is on its way (specs/105) - a seller's own, or staff's for the
 * shop's. The buyer is told the new one and the old one stays on the record. The caller hands in whose mutation it is.
 */
export function CorrectTracking({ current, correct }: { current: string; correct: UseMutationResult<unknown, Error, string> }) {
  const { t } = useTranslation('orders')
  const [open, setOpen] = useState(false)
  const [reference, setReference] = useState(current)
  const wanted = reference.trim()

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setReference(current)
          correct.reset()
        }
      }}
    >
      <DialogTrigger render={<Button variant="ghost" size="sm" className="h-8 rounded-full px-3" />}>
        <PencilIcon /> {t('correctTracking.button')}
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            if (!wanted || wanted === current) return
            correct.mutateAsync(wanted).then(() => {
              toast.success(t('correctTracking.done'))
              setOpen(false)
            }, () => {})
          }}
        >
          <DialogHeader>
            <DialogTitle>{t('correctTracking.title')}</DialogTitle>
            <DialogDescription>{t('correctTracking.body')}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-2">
            <Label htmlFor="correct-tracking">{t('correctTracking.reference')}</Label>
            <Input id="correct-tracking" value={reference} maxLength={100} onChange={(event) => setReference(event.target.value)} />
          </div>
          <ServerError error={correct.error} fallback={t('order.loadFailed')} />
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="ghost" />}>{t('correctTracking.keep')}</DialogClose>
            <Button type="submit" disabled={!wanted || wanted === current || correct.isPending}>
              {t('correctTracking.confirm')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
