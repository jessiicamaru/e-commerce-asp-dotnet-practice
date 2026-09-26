import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { MapPinIcon, PlusIcon } from 'lucide-react'
import { ApiError } from '@/config/axios'
import { AddressDialog } from '@/components/address/address-dialog'
import { AddressChoice } from '@/components/checkout/address-choice'
import { DeliveryChoice } from '@/components/checkout/delivery-choice'
import { VoucherBox } from '@/components/checkout/voucher-box'
import { OrderLines } from '@/components/order/order-lines'
import { OrderTotals } from '@/components/order/order-totals'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { useAddresses } from '@/hooks/address'
import { useCheckoutQuote, usePlaceOrder, useShippingOptions } from '@/hooks/order'
import { money } from '@/utils/shared'

/**
 * Checkout (#38). The customer picks where and how; everything else comes from the server. The
 * breakdown shown is Order's own quote, computed by the same code that then prices the order, so the
 * total here is the total charged unless the cart or a price changes in between.
 *
 * Somebody with no address adds one HERE, in a dialog, and it is chosen as soon as it is saved. The
 * page used to say "you need an address" and link away to a page that did not bring them back.
 */
export function CheckoutPage() {
  const { t } = useTranslation('checkout')
  const navigate = useNavigate()
  const addresses = useAddresses()
  const options = useShippingOptions()
  const placeOrder = usePlaceOrder()

  // What the customer has picked, if anything yet. Until then the choice is DERIVED from what arrived -
  // the default address and the first delivery option - rather than written into state by an effect.
  const [chosenAddressId, setChosenAddressId] = useState<string | null>(null)
  const [chosenShippingOption, setChosenShippingOption] = useState<string | null>(null)

  const addressId =
    chosenAddressId ?? (addresses.data?.find((address) => address.isDefault) ?? addresses.data?.[0])?.id ?? null
  const shippingOption = chosenShippingOption ?? options.data?.[0]?.code ?? null
  // Codes the server has already taken (specs/070): the summary is quoted with them, the order placed with them.
  const [voucherCodes, setVoucherCodes] = useState<string[]>([])

  const where = addressId && shippingOption ? { addressId, shippingOption } : null
  const quote = useCheckoutQuote(where ? { ...where, voucherCodes } : null)

  if (addresses.isError || options.isError) {
    return <ErrorMessage>{t('loadFailed')}</ErrorMessage>
  }

  if (addresses.isPending || options.isPending || !addresses.data || !options.data) {
    return <LoadingRows />
  }

  function place() {
    if (!addressId || !shippingOption) {
      return
    }

    placeOrder.mutate(
      { addressId, shippingOption, voucherCodes },
      { onSuccess: (order) => navigate(`/orders/${order.orderId}`, { state: { justPlaced: true } }) },
    )
  }

  const quoteError = quote.error ? ApiError.from(quote.error) : null

  return (
    <section className="grid gap-6">
      <h1 className="text-2xl font-bold tracking-tight">{t('title')}</h1>

      <div className="grid items-start gap-6 lg:grid-cols-[1fr_24rem]">
        <div className="grid gap-6">
          <Card className="rounded-3xl">
            <CardHeader>
              <CardTitle>{t('deliverTo')}</CardTitle>
            </CardHeader>
            <CardContent>
              {addresses.data.length === 0 ? (
                <div className="grid justify-items-center gap-3 rounded-2xl border border-dashed p-8 text-center">
                  <span className="bg-accent text-accent-foreground grid size-12 place-items-center rounded-2xl">
                    <MapPinIcon className="size-6" />
                  </span>
                  <p className="font-medium">{t('needAddress')}</p>
                  <AddressDialog
                    onSaved={(address) => setChosenAddressId(address.id)}
                    trigger={
                      <Button className="rounded-full px-4 font-semibold">
                        <PlusIcon /> {t('addOne')}
                      </Button>
                    }
                  />
                </div>
              ) : (
                <AddressChoice addresses={addresses.data} addressId={addressId} onChange={setChosenAddressId} />
              )}
            </CardContent>
          </Card>

          <Card className="rounded-3xl">
            <CardHeader>
              <CardTitle>{t('delivery')}</CardTitle>
            </CardHeader>
            <CardContent>
              <DeliveryChoice options={options.data} shippingOption={shippingOption} onChange={setChosenShippingOption} />
            </CardContent>
          </Card>
        </div>

        <Card className="rounded-3xl lg:sticky lg:top-28">
          <CardHeader>
            <CardTitle>{t('summary')}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            {!addressId && <p className="text-muted-foreground text-sm">{t('addressFirst')}</p>}
            {quote.isFetching && !quote.data && <p className="text-muted-foreground text-sm">{t('pricing')}</p>}

            {quoteError && (
              <ErrorMessage>
                {quoteError.status === 409
                  ? (quoteError.problem.detail ?? t('cartRefused'))
                  : quoteError.status === 503
                    ? t('dependencyDown')
                    : t('priceFailed')}{' '}
                <Link to="/cart" className="underline">
                  {t('backToCart')}
                </Link>
              </ErrorMessage>
            )}

            {quote.data && (
              <>
                <OrderLines items={quote.data.items} currency={quote.data.currency} />
                <VoucherBox choice={where} codes={voucherCodes} onChange={setVoucherCodes} />
                <OrderTotals totals={quote.data} shippingName={quote.data.shippingOption.name} />
              </>
            )}
            {placeOrder.isError && <ErrorMessage>{ApiError.from(placeOrder.error).message}</ErrorMessage>}
          </CardContent>
          {quote.data && (
            <CardFooter>
              <Button className="h-11 w-full rounded-full text-base font-semibold" disabled={placeOrder.isPending} onClick={place}>
                {placeOrder.isPending
                  ? t('placing')
                  : t('placeOrder', { amount: money(quote.data.totalAmount, quote.data.currency) })}
              </Button>
            </CardFooter>
          )}
        </Card>
      </div>
    </section>
  )
}
