import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ArrowLeftIcon, ShoppingBagIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ApiError } from '@/config/axios'
import { CartLines } from '@/components/cart/cart-lines'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { useCart, useCartImages, useRemoveCartLine, useSetCartQuantity } from '@/hooks/cart'
import { cn, money } from '@/utils/shared'

/**
 * The cart (#37). It lives in the Cart service, keyed by the signed-in customer, so it survives
 * signing out and back in and follows the customer between devices.
 */
export function CartPage() {
  const { t } = useTranslation('cart')
  const { data: cart, isPending, isError } = useCart()
  const images = useCartImages(cart?.lines ?? [])
  const setQuantity = useSetCartQuantity()
  const removeLine = useRemoveCartLine()
  const busy = setQuantity.isPending || removeLine.isPending

  const failed = (error: unknown) => toast.error(ApiError.from(error).message)

  if (isError) {
    return <ErrorMessage>{t('loadFailed')}</ErrorMessage>
  }

  if (isPending || !cart) {
    return <LoadingRows />
  }

  if (cart.lines.length === 0) {
    return (
      <section className="bg-card ring-border/60 mx-auto grid max-w-md justify-items-center gap-4 rounded-3xl p-10 text-center ring-1">
        <span className="bg-accent text-accent-foreground grid size-14 place-items-center rounded-2xl">
          <ShoppingBagIcon className="size-7" />
        </span>
        <h1 className="text-xl font-bold">{t('empty')}</h1>
        <Link to="/" className={cn(buttonVariants(), 'rounded-full px-5 font-semibold')}>
          {t('browse')}
        </Link>
      </section>
    )
  }

  const units = cart.lines.reduce((sum, line) => sum + line.quantity, 0)

  return (
    <section className="grid gap-6">
      <header className="flex flex-wrap items-end justify-between gap-2">
        <h1 className="text-2xl font-bold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground text-sm">{t('count', { count: units })}</p>
      </header>

      {!cart.pricesAvailable && <ErrorMessage>{t('pricesUnavailable')}</ErrorMessage>}

      <div className="grid items-start gap-6 lg:grid-cols-[1fr_22rem]">
        <Card className="rounded-3xl">
          <CardContent>
            <CartLines
              cart={cart}
              images={images}
              busy={busy}
              onQuantityChange={(variantId, quantity) => setQuantity.mutate([variantId, quantity], { onError: failed })}
              onRemove={(variantId) => removeLine.mutate([variantId], { onError: failed })}
            />
          </CardContent>
        </Card>

        <Card className="rounded-3xl lg:sticky lg:top-28">
          <CardHeader>
            <CardTitle>{t('summary')}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-3">
            <div className="flex items-baseline justify-between gap-3">
              <span className="text-muted-foreground text-sm">{t('subtotal')}</span>
              <span className="text-xl font-bold">
                {cart.estimatedTotal === null ? t('totalUnavailable') : money(cart.estimatedTotal, cart.currency)}
              </span>
            </div>
            <Separator />
            <p className="text-muted-foreground text-xs">{t('estimateNote')}</p>
          </CardContent>
          <CardFooter className="grid gap-2">
            {cart.canCheckOut ? (
              <Link to="/checkout" className={cn(buttonVariants(), 'h-11 rounded-full text-base font-semibold')}>
                {t('checkout')}
              </Link>
            ) : (
              <p className="text-destructive text-sm">{t('fixLines')}</p>
            )}
            <Link to="/" className={cn(buttonVariants({ variant: 'ghost' }), 'rounded-full')}>
              <ArrowLeftIcon /> {t('keepShopping')}
            </Link>
          </CardFooter>
        </Card>
      </div>
    </section>
  )
}
