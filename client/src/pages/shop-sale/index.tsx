import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ChevronRightIcon, MapPinIcon, PhoneIcon } from 'lucide-react'
import { OrderLines } from '@/components/order/order-lines'
import { ParcelActions } from '@/components/order/parcel-actions'
import { ReturnDecision } from '@/components/order/return-decision'
import { SaleEarnings } from '@/components/seller/sale-earnings'
import { Price } from '@/components/shared/price'
import { LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useMoveSale, useSale, useSaleReturn } from '@/hooks/order'
import { describeAddress } from '@/utils/address'
import { sellerReturnStep } from '@/utils/order/returns'

/**
 * One sale: the seller's own lines of one order, and their part of shipping it (specs/034, 035).
 *
 * <p>
 * The delivery address is on this page <b>only while the seller's part is waiting or being prepared</b>
 * - the server stops sending it once their parcel is out (research D6), and the page draws what it is
 * given rather than keeping a copy. The customer's identity and the rest of the order are never here.
 * </p>
 * <p>
 * An order that is not this seller's sale is refused exactly like one that does not exist, and the
 * page repeats the server's words rather than guessing which of the two it was.
 * </p>
 */
export function ShopSalePage() {
  const { t, i18n } = useTranslation('seller')
  const { id = '' } = useParams()
  const sale = useSale(id)
  const { prepare, ship } = useMoveSale(id)
  const { accept, refuse, receive } = useSaleReturn(id)

  const back = (
    <nav className="text-muted-foreground flex items-center gap-1 text-sm">
      <Link to="/shop/sales" className="hover:text-foreground">
        {t('sales.title')}
      </Link>
      <ChevronRightIcon className="size-4" />
      <span className="text-foreground font-mono text-xs">{id.slice(0, 8)}…</span>
    </nav>
  )

  if (sale.isError) {
    return (
      <section className="grid gap-3">
        {back}
        <ServerError error={sale.error} fallback={t('sales.loadFailed')} />
      </section>
    )
  }

  if (sale.isPending) {
    return <LoadingRows />
  }

  const { data } = sale

  return (
    <section className="grid gap-6">
      {back}
      <header className="grid gap-1">
        <h1 className="text-2xl font-bold tracking-tight">
          {t('sales.placedAt', { at: new Date(data.createdAt).toLocaleString(i18n.language) })}
        </h1>
      </header>

      <div className="grid items-start gap-6 lg:grid-cols-[1fr_20rem]">
        <div className="grid gap-6">
          {/* The buyer asked to send their parcel back (specs/066) - first, because it is what waits on them. */}
          {data.return && (
            <Card className="rounded-3xl">
              <CardHeader>
                <CardTitle>{t('return.title')}</CardTitle>
                <CardDescription>{t('return.hint')}</CardDescription>
              </CardHeader>
              <CardContent>
                <ReturnDecision
                  ret={data.return}
                  currency={data.currency}
                  step={sellerReturnStep(data.return)}
                  accept={accept}
                  refuse={refuse}
                  receive={receive}
                />
              </CardContent>
            </Card>
          )}

          <Card className="rounded-3xl">
            <CardHeader>
              <CardTitle>{t('fulfil.title')}</CardTitle>
              <CardDescription>{t('fulfil.hint')}</CardDescription>
            </CardHeader>
            <CardContent>
              {/* Cancelled (specs/039): nothing to prepare or send, and the server refuses a step anyway. */}
              {data.status === 'Cancelled' ? (
                <p className="text-destructive text-sm font-medium">{t('fulfil.cancelled')}</p>
              ) : (
                <ParcelActions status={data.status} trackingReference={data.trackingReference} prepare={prepare} ship={ship} />
              )}
              {data.deliveredAt && (
                <p className="mt-3 text-sm text-emerald-700 dark:text-emerald-400">
                  {t('fulfil.received', { at: new Date(data.deliveredAt).toLocaleDateString(i18n.language) })}
                </p>
              )}
            </CardContent>
          </Card>

          <Card className="rounded-3xl">
            <CardHeader>
              <CardTitle>{t('sales.subtotal')}</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3">
              {/* The order's own currency, frozen at checkout - not whatever the seller is browsing in. */}
              <OrderLines items={data.items} currency={data.currency} />
              <div className="grid justify-items-end gap-1">
                <p className="text-sm">
                  {t('sales.subtotal')}:{' '}
                  <Price value={data.subtotal} currency={data.currency} className="font-semibold" />
                </p>
                <p className="text-muted-foreground max-w-prose text-right text-xs">{t('sales.subtotalHint')}</p>
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="grid gap-6 lg:sticky lg:top-28">
        <Card className="rounded-3xl">
          <CardHeader>
            <CardTitle>{t('earnings.title')}</CardTitle>
          </CardHeader>
          <CardContent>
            <SaleEarnings sale={data} />
          </CardContent>
        </Card>

        <Card className="rounded-3xl">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <MapPinIcon className="size-4.5" /> {t('fulfil.shipTo')}
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-1.5 text-sm">
            {data.shippingAddress ? (
              <>
                <p className="font-semibold">{data.shippingAddress.recipientName}</p>
                <p>{describeAddress(data.shippingAddress)}</p>
                {data.shippingAddress.phone && (
                  <p className="text-muted-foreground flex items-center gap-2">
                    <PhoneIcon className="size-3.5" /> {data.shippingAddress.phone}
                  </p>
                )}
              </>
            ) : (
              <p className="text-muted-foreground">{t('fulfil.addressGone')}</p>
            )}
          </CardContent>
        </Card>
        </div>
      </div>
    </section>
  )
}
