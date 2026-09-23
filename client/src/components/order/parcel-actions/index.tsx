import { useState } from 'react'
import type { UseMutationResult } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckIcon, PackageIcon, TruckIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { cn } from '@/utils/shared'

const STEPS = ['Paid', 'Preparing', 'Shipped'] as const

/**
 * Where one parcel of an order has got to, and the one thing that can be done next (specs/035) - a
 * seller's parcel, or the shop's in the administrator's console (specs/038). The steps and the rule are
 * the same for both, so there is one component; the caller hands in whose mutations these are.
 *
 * <p>
 * Only ever the NEXT step: waiting → preparing → shipped, forwards, one at a time - the same rule the
 * server enforces. Shipping asks for the carrier's tracking reference in a dialog, because it is the
 * one thing the customer follows and it cannot be changed afterwards.
 * </p>
 */
export function ParcelActions({
  status,
  trackingReference,
  prepare,
  ship,
}: {
  /** `Paid` (waiting), `Preparing` or `Shipped` - the PARCEL's, not the order's. */
  status: string
  trackingReference: string | null
  prepare: UseMutationResult<unknown, Error, void>
  ship: UseMutationResult<unknown, Error, string>
}) {
  const { t } = useTranslation('seller')
  const [open, setOpen] = useState(false)
  const [tracking, setTracking] = useState('')
  const reached = STEPS.indexOf(status as (typeof STEPS)[number])

  return (
    <div className="grid gap-4">
      <ol className="grid grid-cols-3 gap-2" aria-label={t('fulfil.progress')}>
        {STEPS.map((step, index) => (
          <li
            key={step}
            aria-current={index === reached ? 'step' : undefined}
            className={cn(
              'flex items-center gap-2 rounded-2xl px-3 py-2 text-sm ring-1',
              index < reached && 'text-muted-foreground ring-border/60',
              index === reached && 'bg-accent text-accent-foreground ring-primary font-semibold',
              index > reached && 'text-muted-foreground ring-border/40 border-dashed',
            )}
          >
            {index < reached ? <CheckIcon className="size-4" /> : <span className="size-4 text-center text-xs">{index + 1}</span>}
            {t(`fulfil.step.${step}`)}
          </li>
        ))}
      </ol>

      {status === 'Paid' && (
        <Button
          className="h-10 justify-self-start rounded-full px-5 font-semibold"
          disabled={prepare.isPending}
          onClick={() => prepare.mutate(undefined, { onSuccess: () => toast.success(t('fulfil.prepared')) })}
        >
          <PackageIcon /> {t('fulfil.prepare')}
        </Button>
      )}

      {status === 'Preparing' && (
        <Dialog
          open={open}
          onOpenChange={(next) => {
            setOpen(next)
            if (next) ship.reset()
          }}
        >
          <DialogTrigger render={<Button className="h-10 justify-self-start rounded-full px-5 font-semibold" />}>
            <TruckIcon /> {t('fulfil.ship')}
          </DialogTrigger>
          <DialogContent className="sm:max-w-md">
            <form
              className="grid gap-4"
              onSubmit={(event) => {
                event.preventDefault()
                ship.mutate(tracking.trim(), {
                  onSuccess: () => {
                    toast.success(t('fulfil.shipped'))
                    setOpen(false)
                  },
                })
              }}
            >
              <DialogHeader>
                <DialogTitle>{t('fulfil.ship')}</DialogTitle>
                <DialogDescription>{t('fulfil.shipHint')}</DialogDescription>
              </DialogHeader>
              <div className="grid gap-2">
                <Label htmlFor="tracking">{t('fulfil.tracking')}</Label>
                <Input
                  id="tracking"
                  autoFocus
                  required
                  maxLength={100}
                  className="h-10 rounded-xl font-mono"
                  placeholder="VNPOST-…"
                  value={tracking}
                  onChange={(event) => setTracking(event.target.value)}
                />
              </div>
              <ServerError error={ship.error} fallback={t('listing.loadFailed')} />
              <DialogFooter>
                <DialogClose render={<Button type="button" variant="ghost" />}>
                  {t('action.cancel', { ns: 'common' })}
                </DialogClose>
                <Button type="submit" disabled={ship.isPending || !tracking.trim()}>
                  {t('fulfil.confirmShip')}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      )}

      {status === 'Shipped' && trackingReference && (
        <p className="text-sm">
          {t('fulfil.trackingIs')} <span className="font-mono font-semibold">{trackingReference}</span>
        </p>
      )}

      <ServerError error={prepare.error} fallback={t('listing.loadFailed')} />
    </div>
  )
}
