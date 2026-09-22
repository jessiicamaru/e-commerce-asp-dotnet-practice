import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '@/config/axios'
import { AddressChoice } from '@/components/checkout/address-choice'
import { DeliveryChoice } from '@/components/checkout/delivery-choice'
import { OrderLines } from '@/components/order/order-lines'
import { OrderTotals } from '@/components/order/order-totals'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { useAddresses } from '@/hooks/address'
import { useCheckoutQuote, usePlaceOrder, useShippingOptions } from '@/hooks/order'
import { money } from '@/utils/shared'

/**
 * Checkout (#38). The customer picks where and how; everything else comes from the server. The
 * breakdown shown is Order's own quote, computed by the same code that then prices the order, so the
 * total here is the total charged unless the cart or a price changes in between.
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

  const quote = useCheckoutQuote(addressId && shippingOption ? { addressId, shippingOption } : null)

  if (addresses.isError || options.isError) {
    return <ErrorMessage>{t('loadFailed')}</ErrorMessage>
  }

  if (addresses.isPending || options.isPending || !addresses.data || !options.data) {
    return <LoadingRows />
  }

  if (addresses.data.length === 0) {
    return (
      <section>
        <h1 className="mb-4 text-2xl font-bold">{t('title')}</h1>
        <p>
          {t('needAddress')}{' '}
          <Link to="/addresses" className="underline">
            {t('addOne')}
          </Link>
          .
        </p>
      </section>
    )
  }

  function place() {
    if (!addressId || !shippingOption) {
      return
    }

    placeOrder.mutate(
      { addressId, shippingOption },
      { onSuccess: (order) => navigate(`/orders/${order.orderId}`, { state: { justPlaced: true } }) },
    )
  }

  const quoteError = quote.error ? ApiError.from(quote.error) : null

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold">{t('title')}</h1>

      <AddressChoice addresses={addresses.data} addressId={addressId} onChange={setChosenAddressId} />
      <DeliveryChoice options={options.data} shippingOption={shippingOption} onChange={setChosenShippingOption} />

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
          <OrderTotals totals={quote.data} shippingName={quote.data.shippingOption.name} />
          {placeOrder.isError && <ErrorMessage>{ApiError.from(placeOrder.error).message}</ErrorMessage>}
          <Button className="self-start" disabled={placeOrder.isPending} onClick={place}>
            {placeOrder.isPending ? t('placing') : t('placeOrder', { amount: money(quote.data.totalAmount, quote.data.currency) })}
          </Button>
        </>
      )}
    </section>
  )
}
