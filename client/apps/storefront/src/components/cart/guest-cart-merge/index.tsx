import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { Cart } from '@ecommerce/core/services/cart'
import { clearGuestCart, readGuestCart } from '@ecommerce/core/utils/cart/guest-cart'

/**
 * Moves this browser's cart into the account's whenever somebody is signed in (specs/162 research D3): after a password,
 * a two-factor code or a registration, or a session restored on load - one rule for every way in. The server takes the
 * larger quantity per shape, so a merge sent twice (two tabs, a retry) changes nothing; the browser's cart is emptied
 * only once the server has it, and kept for the next try when the merge fails.
 */
export function GuestCartMerge() {
  const { t } = useTranslation('cart')
  const { user } = useAuth()
  const queryClient = useQueryClient()
  // Who this tab already merged for - not twice for one sign-in, whatever re-renders.
  const mergedFor = useRef<string | null>(null)

  useEffect(() => {
    if (!user) {
      mergedFor.current = null
      return
    }
    const lines = readGuestCart()
    if (lines.length === 0 || mergedFor.current === user.id) return
    mergedFor.current = user.id

    Cart.merge(lines)
      .then(async () => {
        clearGuestCart()
        await queryClient.invalidateQueries({ queryKey: queryKeys.cart() })
        toast.success(t('merged'))
      })
      .catch(() => {
        // Kept in the browser; the next sign-in or reload tries again.
        mergedFor.current = null
      })
  }, [user, queryClient, t])

  return null
}
