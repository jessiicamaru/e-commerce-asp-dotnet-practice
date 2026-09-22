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
  const { data: cart, isPending, isError } = useCart()
  const setQuantity = useSetCartQuantity()
  const removeLine = useRemoveCartLine()
  const busy = setQuantity.isPending || removeLine.isPending

  const failed = (error: unknown) => toast.error(ApiError.from(error).message)

  if (isError) {
    return <ErrorMessage>The cart could not be loaded.</ErrorMessage>
  }

  if (isPending || !cart) {
    return <LoadingRows />
  }

  if (cart.lines.length === 0) {
    return (
      <section>
        <h1 className="mb-4 text-2xl font-bold">Your cart</h1>
        <p>
          Your cart is empty.{' '}
          <Link to="/" className="underline">
            Browse the shop
          </Link>
          .
        </p>
      </section>
    )
  }

  return (
    <section>
      <h1 className="mb-4 text-2xl font-bold">Your cart</h1>

      {!cart.pricesAvailable && (
        <ErrorMessage>Prices could not be checked just now. Your items are safe; try again in a moment.</ErrorMessage>
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
        {cart.estimatedTotal === null ? 'Total unavailable' : `Estimated ${money(cart.estimatedTotal)}`}
      </p>
      <p className="text-muted-foreground text-xs">
        An estimate from today's prices, before shipping and tax. The final amount is fixed at checkout.
      </p>

      {cart.canCheckOut ? (
        <Link to="/checkout" className={cn(buttonVariants(), 'mt-4')}>
          Check out
        </Link>
      ) : (
        <p className="text-destructive mt-4 text-sm">Remove or fix the items marked above before checking out.</p>
      )}
    </section>
  )
}
