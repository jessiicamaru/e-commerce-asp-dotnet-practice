import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Address } from '@/services/address'
import type { AddressFields } from '@/services/address/types'

export function useAddresses(enabled = true) {
  return useQuery({ queryKey: queryKeys.addresses(), queryFn: () => Address.list(), enabled })
}

function useAddressMutation<TVariables>(action: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: action,
    // Identity decides which address is the default, so the list is re-read rather than patched.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.addresses() }),
  })
}

export const useSaveAddress = () =>
  useAddressMutation(({ id, fields }: { id?: string; fields: AddressFields }) =>
    id ? Address.update(id, fields) : Address.create(fields),
  )

export const useDeleteAddress = () => useAddressMutation((id: string) => Address.remove(id))

export const useMakeAddressDefault = () => useAddressMutation((id: string) => Address.makeDefault(id))
