import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { BanIcon } from 'lucide-react'
import { toast } from 'sonner'
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
import { useShopMoves } from '@ecommerce/core/hooks/shop'

/**
 * Staff close a shop without touching its seller's account (specs/107): its products leave the shelf, the seller reads
 * the reason, and only staff reopen it - from the "Closed shops" tab, where this sends them once it is done, since a
 * closed shop's page is a 404 to everybody.
 */
export function CloseShop({
  sellerId,
  shopName,
  open,
  onOpenChange,
}: {
  sellerId: string
  shopName: string
  /** Opened from elsewhere - a menu (specs/137) - rather than by its own button. */
  open?: boolean
  onOpenChange?: (open: boolean) => void
}) {
  const { t } = useTranslation('admin')
  const navigate = useNavigate()
  const { close } = useShopMoves()
  const [reason, setReason] = useState('')

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {open === undefined && (
        <DialogTrigger render={<Button variant="outline" className="text-destructive justify-self-start rounded-full" />}>
          <BanIcon /> {t('shops.close.button')}
        </DialogTrigger>
      )}
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('shops.close.title', { shop: shopName })}</DialogTitle>
          <DialogDescription>{t('shops.close.body')}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-1.5">
          <Label htmlFor="close-reason">{t('shops.close.reason')}</Label>
          <Textarea id="close-reason" value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
        </div>
        <ServerError error={close.error} fallback={t('shops.close.failed')} />
        <DialogFooter>
          <DialogClose render={<Button variant="ghost" className="rounded-full" />}>{t('shops.close.keep')}</DialogClose>
          <Button
            variant="destructive"
            className="rounded-full"
            disabled={close.isPending || !reason.trim()}
            onClick={() =>
              close.mutateAsync({ sellerId, reason: reason.trim() }).then(() => {
                toast.success(t('shops.close.done', { shop: shopName }))
                navigate('/shops?status=Closed')
              }, () => {})
            }
          >
            {t('shops.close.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
