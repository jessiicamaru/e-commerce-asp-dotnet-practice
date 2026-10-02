import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { ShopApplications } from '@ecommerce/core/services/shop-applications'
import type { ShopApplicationInput, ShopApplicationStatus } from '@ecommerce/core/services/shop-applications/types'

export function useMyShopApplications() {
  return useQuery({ queryKey: queryKeys.myShopApplications, queryFn: () => ShopApplications.mine() })
}

export function useApplyForShop() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: ShopApplicationInput) => ShopApplications.apply(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.myShopApplications }),
  })
}

/** A page of the queue or the history. Keeps the previous page on screen while the next loads. */
export function useShopApplications(status: ShopApplicationStatus, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.shopApplications(status, page, pageSize),
    queryFn: () => ShopApplications.list(status, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/** Approve or reject; every list is re-read, since the application has moved between them. */
export function useDecideShopApplication() {
  const queryClient = useQueryClient()
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['shop-applications'] })
    // The counts on the sidebar, the dashboard and the overview (specs/130).
    await queryClient.invalidateQueries({ queryKey: ['staff-waiting'] })
  }
  return {
    approve: useMutation({ mutationFn: (id: string) => ShopApplications.approve(id), onSettled: refresh }),
    reject: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) => ShopApplications.reject(id, reason),
      onSettled: refresh,
    }),
  }
}
