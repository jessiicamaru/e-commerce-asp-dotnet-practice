import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { ApiError } from '@/config/axios'
import { CartLines } from '@/components/cart/cart-lines'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { buttonVariants } from '@/components/ui/button'
import { useCart, useRemoveCartLine, useSetCartQuantity } from '@/hooks/cart'
import { cn, money } from '@/utils/shared'

/**
 * The cart (#37). It lives in the Cart service, keyed by the signed-in customer, so it survives
 * signing out and back in and follows the customer between devices.
 */
export function CartPage() {
  const { t } = useTranslation('cart')
  const { data: cart, isPending, isError } = useCart()
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
      <section>
        <h1 className="mb-4 text-2xl font-bold">{t('title')}</h1>
        <p>
          {t('empty')}{' '}
          <Link to="/" className="underline">
            {t('browse')}
          </Link>
          .
        </p>
      </section>
    )
  }

  return (
    <section>
      <h1 className="mb-4 text-2xl font-bold">{t('title')}</h1>

      {!cart.pricesAvailable && (
        <ErrorMessage>{t('pricesUnavailable')}</ErrorMessage>
      )}

      <CartLines
        cart={cart}
        busy={busy}
        onQuantityChange={(productId, quantity) =>
          setQuantity.mutate([productId, quantity], { onError: failed })
        }
        onRemove={(productId) => removeLine.mutate([productId], { onError: failed })}
      />

      <p className="mt-4 text-xl font-semibold">
        {cart.estimatedTotal === null
          ? t('totalUnavailable')
          : t('estimated', { amount: money(cart.estimatedTotal, cart.currency) })}
      </p>
      <p className="text-muted-foreground text-xs">{t('estimateNote')}</p>

      {cart.canCheckOut ? (
        <Link to="/checkout" className={cn(buttonVariants(), 'mt-4')}>
          {t('checkout')}
        </Link>
      ) : (
        <p className="text-destructive mt-4 text-sm">{t('fixLines')}</p>
      )}
    </section>
  )
}
