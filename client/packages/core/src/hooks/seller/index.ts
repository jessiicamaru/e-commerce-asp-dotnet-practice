import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Seller } from '@ecommerce/core/services/seller'
import type { PayoutAccountInput } from '@ecommerce/core/services/seller/types'

/** `enabled` keeps a customer from asking for a shop they do not have and collecting a 403. */
export function useMyShop(enabled: boolean) {
  return useQuery({ queryKey: queryKeys.myShop(), queryFn: () => Seller.me(), enabled, retry: false })
}

export function useRenameShop() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (shopName: string) => Seller.rename(shopName),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.myShop() })
      // The name under every listing comes from Catalog's read model, which the rename fed through
      // the broker. Re-reading the listings is how this page catches up with that - it is NOT
      // instant, and pretending otherwise by patching the cache would show a name the catalogue
      // has not got yet.
      await queryClient.invalidateQueries({ queryKey: ['my-products'] })
      await queryClient.invalidateQueries({ queryKey: ['products'] })
    },
  })
}

/** The shop's description (specs/099). Its page reads Catalog's copy, fed through the broker - not instant. */
export function useDescribeShop() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (description: string | null) => Seller.describe(description),
    onSuccess: async (shop) => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.myShop() })
      await queryClient.invalidateQueries({ queryKey: queryKeys.shopFront(shop.sellerId) })
    },
  })
}

/** Where the caller's payouts go (specs/106) - null until they give one. */
export function useMyPayoutAccount() {
  return useQuery({ queryKey: queryKeys.myPayoutAccount(), queryFn: () => Seller.payoutAccount() })
}

export function useSetPayoutAccount() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: PayoutAccountInput) => Seller.setPayoutAccount(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.myPayoutAccount() }),
  })
}
