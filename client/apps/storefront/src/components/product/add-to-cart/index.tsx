import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'
import { ShoppingBagIcon } from 'lucide-react'
import { toast } from 'sonner'
import { QuantityStepper } from '@/components/shared/quantity-stepper'
import { Button } from '@ecommerce/ui/button'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { GuestCartUnavailable, useAddToCart } from '@ecommerce/core/hooks/cart'
import { ApiError } from '@ecommerce/core/config/axios'

/**
 * Adding to the cart, signed in or not: signed out, the lines are kept in this browser and merged into the account's
 * cart at sign-in (specs/162, #370). Before that, adding needed an account.
 *
 * <p>
 * The button is shown to everybody (specs/126, #252) - a buy button hidden until you have an account loses the people
 * deciding whether to make one. Signed out, pressing it opens sign-in with a reason, and the way back carries the
 * chosen variant (`?variant=`), so the shopper returns to the same choice. Nothing is added before signing in.
 * </p>
 */
export function AddToCart({
  productId,
  variantId,
  disabledReason,
  available,
}: {
  productId: string
  /** Which shape to add. Undefined only while the customer has not chosen one yet. */
  variantId?: string
  /** Why the button is disabled, shown next to it - "Choose an option first". */
  disabledReason?: string
  /** Inventory's available count, when known: the plus stops there, and none left disables adding. */
  available?: number | null
}) {
  const { t } = useTranslation('catalog')
  const { user, restoring } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [quantity, setQuantity] = useState(1)
  const addToCart = useAddToCart()

  if (restoring) {
    return null
  }

  const soldOut = available !== undefined && available !== null && available <= 0
  const reason = disabledReason ?? (soldOut ? t('stock.out') : undefined)

  // Signed out it goes into this browser's cart (specs/162); only a browser that cannot keep one (storage blocked or
  // full) sends the shopper to sign in, with the way back carrying the chosen variant (specs/126).
  const add = () =>
    addToCart.mutate([productId, quantity, variantId], {
      onSuccess: () =>
        toast.success(t('product.added', { count: quantity }), {
          action: { label: t('product.viewCart'), onClick: () => navigate('/cart') },
        }),
      onError: (error) =>
        !user && error instanceof GuestCartUnavailable
          ? navigate('/sign-in', {
              state: { from: variantId ? `${location.pathname}?variant=${variantId}` : location.pathname, reason: 'cart' },
            })
          : toast.error(error instanceof ApiError ? error.message : t('product.addFailed')),
    })

  return (
    <div className="flex flex-wrap items-center gap-3">
      <QuantityStepper
        label={t('product.quantity')}
        value={quantity}
        max={available && available > 0 ? available : undefined}
        disabled={reason !== undefined}
        onChange={setQuantity}
      />
      <Button className="h-10 rounded-full px-5 font-semibold" onClick={add} disabled={addToCart.isPending || reason !== undefined}>
        <ShoppingBagIcon /> {addToCart.isPending ? t('product.adding') : t('product.addToCart')}
      </Button>
      {reason && <span className="text-muted-foreground text-sm">{reason}</span>}
    </div>
  )
}
