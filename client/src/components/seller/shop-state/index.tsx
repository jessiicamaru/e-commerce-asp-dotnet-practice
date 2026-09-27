import { useTranslation } from 'react-i18next'
import { PauseIcon, PlayIcon, StoreIcon } from 'lucide-react'
import { toast } from 'sonner'
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
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useShopMoves, useShopState } from '@/hooks/shop'

/**
 * Whether the seller's shop is on the shelf, and the one thing they can do about it (specs/107): pause it while away,
 * or reopen it. A shop staff closed shows the reason and no button - only staff reopen it, and the server refuses the
 * seller anyway; hiding the button is for drawing, not deciding.
 */
export function ShopStateCard() {
  const { t, i18n } = useTranslation('seller')
  const shop = useShopState(true)
  const { pause, resume } = useShopMoves()

  // A shop Catalog has not heard of yet (its name is on the broker) has nothing to pause; say nothing.
  if (!shop.data) return null
  const { state, pausedAt, closedAt, closedReason } = shop.data
  const failed = pause.error ?? resume.error

  return (
    <div className="bg-card ring-border/60 grid gap-3 rounded-3xl p-5 ring-1">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2 font-semibold">
          <StoreIcon className="size-4.5" /> {t('shopState.title')}
          <Badge variant={state === 'Open' ? 'secondary' : 'destructive'}>{t(`shopState.state.${state}`)}</Badge>
        </div>

        {state === 'Open' && (
          <AlertDialog>
            <AlertDialogTrigger render={<Button variant="outline" className="rounded-full" disabled={pause.isPending} />}>
              <PauseIcon /> {t('shopState.pause')}
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>{t('shopState.pauseTitle')}</AlertDialogTitle>
                <AlertDialogDescription>{t('shopState.pauseBody')}</AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>{t('shopState.keepOpen')}</AlertDialogCancel>
                <AlertDialogAction
                  onClick={() => pause.mutateAsync().then(() => toast.success(t('shopState.paused')), () => {})}
                >
                  {t('shopState.pause')}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        )}

        {state === 'Paused' && (
          <Button
            className="rounded-full"
            disabled={resume.isPending}
            onClick={() => resume.mutateAsync().then(() => toast.success(t('shopState.reopened')), () => {})}
          >
            <PlayIcon /> {t('shopState.reopen')}
          </Button>
        )}
      </div>

      {state === 'Paused' && pausedAt && (
        <p className="text-muted-foreground text-sm">
          {t('shopState.pausedSince', { at: new Date(pausedAt).toLocaleString(i18n.language) })}
        </p>
      )}
      {state === 'Closed' && closedAt && (
        <p className="text-sm" role="alert">
          {t('shopState.closedBecause', { at: new Date(closedAt).toLocaleString(i18n.language), reason: closedReason ?? '' })}
        </p>
      )}
      {state === 'Suspended' && <p className="text-sm">{t('shopState.suspended')}</p>}
      <ServerError error={failed} fallback={t('shopState.failed')} />
    </div>
  )
}
