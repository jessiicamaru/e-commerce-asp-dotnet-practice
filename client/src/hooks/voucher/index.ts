import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Order } from '@/services/order'
import type { CheckoutChoice } from '@/services/order/types'
import { Voucher } from '@/services/voucher'
import type { NewVoucher } from '@/services/voucher/types'

/** The caller's vouchers, a page at a time. */
export function useMyVouchers(page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.myVouchers(page),
    queryFn: () => Voucher.mine(page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/** Creates one, then re-reads the list - what it now holds is the server's to say. */
export function useCreateVoucher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (voucher: NewVoucher) => Voucher.create(voucher),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['vouchers'] }),
  })
}

export function useDisableVoucher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => Voucher.disable(id),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['vouchers'] }),
  })
}

/**
 * Tries a code before keeping it (specs/070 research D1): the checkout quote with the codes already applied plus
 * this one. A refusal is the server's words about THAT code, shown where it was typed - the summary's own quote
 * never sees a code that would break it.
 */
export function useTryVoucher() {
  return useMutation({
    mutationFn: ({ choice, code }: { choice: CheckoutChoice; code: string }) =>
      Order.quote({ ...choice, voucherCodes: [...(choice.voucherCodes ?? []), code] }),
  })
}
