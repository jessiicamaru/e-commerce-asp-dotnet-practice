import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ChevronRightIcon, MapPinIcon, PhoneIcon } from 'lucide-react'
import { CancelOrder } from '@/components/order/cancel-order'
import { OrderLines } from '@/components/order/order-lines'
import { OrderShipments } from '@/components/order/order-shipments'
import { OrderTotals } from '@/components/order/order-totals'
import { ParcelActions } from '@/components/order/parcel-actions'
import { LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useMoveShopParcel, useStaffCancelOrder, useStaffOrder } from '@/hooks/admin'
import { staffCanCancel } from '@/utils/order/cancel'
import { describeAddress } from '@/utils/address'
import { shopParcelOf } from './shop-parcel'

/**
 * One order as staff see it (specs/038): the shop's parcel and the next step for it, where it goes, and
 * the whole order - including the parcels sellers send, so staff can see those are not theirs to move.
 */
export function AdminOrderPage() {
  const { t, i18n } = useTranslation('admin')
  const { id = '' } = useParams()
  const order = useStaffOrder(id)
  const { prepare, ship } = useMoveShopParcel(id)
  const cancel = useStaffCancelOrder(id)

  const back = (
    <nav className="text-muted-foreground flex items-center gap-1 text-sm">
      <Link to="/admin" className="hover:text-foreground">
        {t('order.back')}
      </Link>
      <ChevronRightIcon className="size-4" />
      <span className="text-foreground font-mono text-xs">{id.slice(0, 8)}…</span>
    </nav>
  )

  if (order.isError) {
    return (
      <section className="grid gap-3">
        {back}
        <ServerError error={order.error} fallback={t('order.loadFailed')} />
      </section>
    )
  }

  if (order.isPending) {
    return <LoadingRows />
  }

  const { data } = order
  const cancelled = data.status === 'Cancelled'
  const parcel = cancelled ? null : shopParcelOf(data)
  const address = data.shippingAddress

  return (
    <section className="grid gap-6">
      {back}
      <h1 className="text-2xl font-bold tracking-tight">
        {t('queue.placedAt', { at: new Date(data.createdAt).toLocaleString(i18n.language) })}
      </h1>

      <div className="grid items-start gap-6 lg:grid-cols-[1fr_20rem]">
        <div className="grid gap-6">
          <Card className="rounded-3xl">
            <CardHeader>
              <CardTitle>{t('order.shopParcel')}</CardTitle>
              <CardDescription>{t('order.shopParcelHint')}</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4">
              {parcel ? (
                <>
                  <div className="grid gap-1">
                    <p className="text-muted-foreground text-xs">{t('order.toPack')}</p>
                    <ul className="list-inside list-disc text-sm">
                      {parcel.items.map((item) => (
                        <li key={item}>{item}</li>
                      ))}
                    </ul>
                  </div>
                  <ParcelActions status={parcel.status} trackingReference={parcel.trackingReference} prepare={prepare} ship={ship} />
                </>
              ) : (
                <p className="text-muted-foreground text-sm">
                  {cancelled
                    ? t(data.cancelledBy === 'Customer' ? 'order.cancelledByCustomer' : 'order.cancelledByStaff')
                    : t('order.noShopGoods')}
                </p>
              )}
              {staffCanCancel(data) && <CancelOrder cancel={cancel} />}
            </CardContent>
          </Card>

          {!cancelled && <OrderShipments shipments={data.shipments ?? []} />}

          <Card className="rounded-3xl">
            <CardHeader>
              <CardTitle>{t('order.lines')}</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4">
              <OrderLines items={data.items} currency={data.currency} />
              <OrderTotals totals={data} shippingName={data.shippingOption?.name} />
            </CardContent>
          </Card>
        </div>

        <Card className="rounded-3xl lg:sticky lg:top-28">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <MapPinIcon className="size-4.5" /> {t('order.shipTo')}
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-1.5 text-sm">
            {address ? (
              <>
                <p className="font-semibold">{address.recipientName}</p>
                <p>{describeAddress(address)}</p>
                {address.phone && (
                  <p className="text-muted-foreground flex items-center gap-2">
                    <PhoneIcon className="size-3.5" /> {address.phone}
                  </p>
                )}
              </>
            ) : (
              <p className="text-muted-foreground">{t('order.noAddress')}</p>
            )}
          </CardContent>
        </Card>
      </div>
    </section>
  )
}
