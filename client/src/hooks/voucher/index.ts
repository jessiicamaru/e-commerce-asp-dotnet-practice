import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Order } from '@/services/order'
import type { CheckoutChoice } from '@/services/order/types'
import { Voucher } from '@/services/voucher'
import type { NewVoucher, PublicVoucherScope, VoucherEdit } from '@/services/voucher/types'

/** The caller's vouchers, a page at a time. */
export function useMyVouchers(page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.myVouchers(page),
    queryFn: () => Voucher.mine(page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/** The public vouchers a page could show (specs/114); nothing is asked while the scope is unknown. */
export function usePublicVouchers(scope: PublicVoucherScope | null) {
  return useQuery({
    queryKey: queryKeys.publicVouchers(scope),
    queryFn: () => Voucher.public(scope!),
    enabled: scope !== null && (scope.platform === true || (scope.sellerIds?.length ?? 0) > 0),
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

/** Corrects a voucher's terms (specs/113), then re-reads the list. */
export function useEditVoucher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, edit }: { id: string; edit: VoucherEdit }) => Voucher.edit(id, edit),
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
