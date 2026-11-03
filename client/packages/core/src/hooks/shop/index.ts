import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Shops } from '@ecommerce/core/services/shops'

/** The caller's own shop state (specs/107). `enabled` keeps a customer from asking and collecting a 403. */
export function useShopState(enabled: boolean) {
  return useQuery({ queryKey: queryKeys.shopState(), queryFn: () => Shops.mine(), enabled, retry: false })
}

/**
 * Any shop's public page (specs/099) - read by the shop page and, for the shop's rating beside "Sold by" (specs/165), by
 * a seller's product page, under one key. Nothing is asked without a seller: the shop's own goods have no shop page.
 */
export function useShopFront(sellerId: string | null | undefined) {
  return useQuery({
    queryKey: queryKeys.shopFront(sellerId ?? ''),
    queryFn: () => Shops.get(sellerId!),
    enabled: !!sellerId,
    retry: false,
  })
}

/** Shops staff closed, newest first - the "Closed shops" tab. */
export function useClosedShops(page: number, pageSize: number) {
  return useQuery({ queryKey: queryKeys.closedShops(page), queryFn: () => Shops.closed(page, pageSize) })
}

/**
 * Every move of a shop's state (specs/107). Each changes what the shop page, the listing and the staff list show, so
 * each refreshes all of them rather than guessing which one moved.
 */
export function useShopMoves() {
  const queryClient = useQueryClient()
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.shopState() }),
      queryClient.invalidateQueries({ queryKey: ['shops', 'closed'] }),
      queryClient.invalidateQueries({ queryKey: ['shop-front'] }),
      queryClient.invalidateQueries({ queryKey: ['products'] }),
    ])

  return {
    pause: useMutation({ mutationFn: () => Shops.pause(), onSuccess: refresh }),
    resume: useMutation({ mutationFn: () => Shops.resume(), onSuccess: refresh }),
    close: useMutation({
      mutationFn: ({ sellerId, reason }: { sellerId: string; reason: string }) => Shops.close(sellerId, reason),
      onSuccess: refresh,
    }),
    reopen: useMutation({ mutationFn: (sellerId: string) => Shops.reopen(sellerId), onSuccess: refresh }),
  }
}
