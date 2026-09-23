import { useTranslation } from 'react-i18next'
import { PackageIcon, PackageCheckIcon, TruckIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import type { Shipment } from '@/services/order/types'
import { cn } from '@/utils/shared'

/**
 * An order that goes in several parcels, parcel by parcel (specs/035): what is in each, where it has
 * got to, and its tracking reference - so a customer can tell "half sent" from "sent".
 *
 * Drawn only for two or more: an order in one parcel reads exactly as it did before parts existed,
 * with its single tracking reference on the order.
 */
export function OrderShipments({ shipments }: { shipments: Shipment[] }) {
  const { t } = useTranslation('orders')

  if (shipments.length < 2) {
    return null
  }

  return (
    <section className="grid gap-3" aria-label={t('parcels.title')}>
      <h2 className="text-lg font-semibold">
        {t('parcels.heading', {
          shipped: shipments.filter((s) => s.status === 'Shipped').length,
          count: shipments.length,
        })}
      </h2>
      <ul className="grid gap-2 sm:grid-cols-2">
        {shipments.map((shipment, index) => {
          const Icon = shipment.status === 'Shipped' ? TruckIcon : shipment.status === 'Preparing' ? PackageCheckIcon : PackageIcon

          return (
            <li key={index} className="bg-card ring-border/60 grid gap-2 rounded-2xl p-4 ring-1">
              <div className="flex items-center justify-between gap-2">
                <span className="flex items-center gap-2 font-medium">
                  <Icon className="text-muted-foreground size-4.5" />
                  <span className="grid">
                    {t('parcels.number', { index: index + 1, count: shipments.length })}
                    {/* Who is sending it (specs/036). The shop's own is worded here, in the reader's
                        language; a seller's name was frozen at checkout. Neither known: nothing, not a guess. */}
                    {(shipment.isShop || shipment.sellerName) && (
                      <span className="text-muted-foreground text-xs font-normal">
                        {t('parcels.from', { shop: shipment.isShop ? t('parcels.theShop') : shipment.sellerName })}
                      </span>
                    )}
                  </span>
                </span>
                <Badge
                  variant="outline"
                  className={cn(shipment.status === 'Shipped' && 'border-emerald-600/40 text-emerald-700 dark:text-emerald-400')}
                >
                  {t(`parcels.status.${shipment.status}`, { defaultValue: shipment.status })}
                </Badge>
              </div>
              <ul className="text-muted-foreground grid gap-0.5 text-sm">
                {shipment.items.map((item, i) => (
                  <li key={i}>{item}</li>
                ))}
              </ul>
              {shipment.trackingReference && (
                <p className="text-xs">
                  {t('parcels.tracking')} <span className="font-mono font-semibold">{shipment.trackingReference}</span>
                </p>
              )}
            </li>
          )
        })}
      </ul>
    </section>
  )
}
