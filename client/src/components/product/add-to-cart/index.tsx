import { useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ShoppingBagIcon } from 'lucide-react'
import { toast } from 'sonner'
import { QuantityStepper } from '@/components/shared/quantity-stepper'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/context/auth/useAuth'
import { useAddToCart } from '@/hooks/cart'
import { ApiError } from '@/config/axios'

/** Adding needs an account: the cart is kept per customer by the Cart service, not in the browser. */
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

  if (!user) {
    return (
      <p className="text-sm">
        <Trans
          t={t}
          i18nKey="product.signInToAdd"
          components={[<Link key="0" to="/sign-in" state={{ from: location.pathname }} className="underline" />]}
        />
      </p>
    )
  }

  const soldOut = available !== undefined && available !== null && available <= 0
  const reason = disabledReason ?? (soldOut ? t('stock.out') : undefined)

  const add = () =>
    addToCart.mutate([productId, quantity, variantId], {
      onSuccess: () =>
        toast.success(t('product.added', { count: quantity }), {
          action: { label: t('product.viewCart'), onClick: () => navigate('/cart') },
        }),
      onError: (error) =>
        toast.error(error instanceof ApiError ? error.message : t('product.addFailed')),
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
