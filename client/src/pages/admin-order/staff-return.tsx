import { useTranslation } from 'react-i18next'
import { ReturnDecision } from '@/components/order/return-decision'
import { Badge } from '@/components/ui/badge'
import { useStaffReturn } from '@/hooks/admin'
import type { ParcelReturn, Shipment } from '@/services/order/types'
import { isFinalDecision, staffReturnStep } from '@/utils/order/returns'

/**
 * One parcel's return as staff see it (specs/067): the shop's own, which staff answer and receive like a
 * seller, or a seller's - which staff only decide once the buyer has escalated it, and then for good.
 */
export function StaffReturn({ orderId, shipment, currency }: { orderId: string; shipment: Shipment & { return: ParcelReturn }; currency: string }) {
  const { t } = useTranslation('admin')
  const { accept, refuse, receive } = useStaffReturn(orderId, shipment.id ?? '')
  const ret = shipment.return

  return (
    <li className="ring-border/60 grid gap-3 rounded-2xl p-4 ring-1">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="font-medium">
          {shipment.isShop ? t('returns.shopParcel') : t('returns.sellerParcel', { shop: shipment.sellerName ?? '—' })}
        </span>
        <Badge variant="outline">{t(`returns.state.${ret.status}`, { defaultValue: ret.status })}</Badge>
      </div>
      <ReturnDecision
        ret={ret}
        currency={currency}
        step={staffReturnStep(ret)}
        final={isFinalDecision(ret)}
        accept={accept}
        refuse={refuse}
        receive={receive}
      />
      {!ret.isShop && ret.status !== 'Escalated' && (
        <p className="text-muted-foreground text-xs">{t('returns.sellersToAnswer')}</p>
      )}
    </li>
  )
}
