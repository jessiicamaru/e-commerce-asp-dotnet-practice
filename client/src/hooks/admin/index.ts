import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Admin } from '@/services/admin'
import type { QueueState } from '@/services/admin/types'

/** One state's queue, a page at a time. Keeps the previous page on screen while the next loads. */
export function useFulfilmentQueue(status: QueueState, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.adminQueue(status, page),
    queryFn: () => Admin.fulfilment(status, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/** One order, as staff see it. A 404 will not become something else on a retry. */
export function useStaffOrder(id: string) {
  return useQuery({ queryKey: queryKeys.adminOrder(id), queryFn: () => Admin.order(id), enabled: id !== '', retry: false })
}

/**
 * The shop parcel's two steps. Both re-read the order and every queue rather than patching them: the
 * server decides where the parcel and the order have got to.
 */
export function useMoveShopParcel(id: string) {
  const queryClient = useQueryClient()
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.adminOrder(id) })
    await queryClient.invalidateQueries({ queryKey: ['admin-queue'] })
  }

  return {
    prepare: useMutation({ mutationFn: () => Admin.prepare(id), onSuccess: refresh }),
    ship: useMutation({ mutationFn: (trackingReference: string) => Admin.ship(id, trackingReference), onSuccess: refresh }),
  }
}

/** Staff cancel an order (specs/039); the order and every queue are re-read. */
export function useStaffCancelOrder(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => Admin.cancel(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.adminOrder(id) })
      await queryClient.invalidateQueries({ queryKey: ['admin-queue'] })
    },
  })
}

export function usePayoutsDue() {
  return useQuery({ queryKey: queryKeys.payoutsDue(), queryFn: () => Admin.due() })
}

/** Records a payout, then re-reads the due list - whatever happened, it has changed. */
export function usePaySeller() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ sellerId, currency }: { sellerId: string; currency: string }) => Admin.pay(sellerId, currency),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.payoutsDue() }),
  })
}
