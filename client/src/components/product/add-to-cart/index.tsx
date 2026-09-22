import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useAuth } from '@/context/auth/useAuth'
import { useAddToCart } from '@/hooks/cart'
import { ApiError } from '@/config/axios'

/** Adding needs an account: the cart is kept per customer by the Cart service, not in the browser. */
export function AddToCart({ productId }: { productId: string }) {
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
        <Link to="/sign-in" state={{ from: location.pathname }} className="underline">
          Sign in
        </Link>{' '}
        to add this to your cart.
      </p>
    )
  }

  const add = () =>
    addToCart.mutate([productId, quantity], {
      onSuccess: () =>
        toast.success(`Added ${quantity} to your cart.`, {
          action: { label: 'View cart', onClick: () => navigate('/cart') },
        }),
      onError: (error) =>
        toast.error(error instanceof ApiError ? error.message : 'It could not be added. Try again.'),
    })

  return (
    <div className="flex items-center gap-3">
      <Input
        type="number"
        min={1}
        aria-label="Quantity"
        className="w-20"
        value={quantity}
        onChange={(event) => setQuantity(Math.max(1, Math.floor(Number(event.target.value)) || 1))}
      />
      <Button onClick={add} disabled={addToCart.isPending}>
        {addToCart.isPending ? 'Adding…' : 'Add to cart'}
      </Button>
    </div>
  )
}
