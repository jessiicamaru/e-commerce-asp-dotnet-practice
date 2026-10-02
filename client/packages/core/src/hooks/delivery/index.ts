import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Delivery } from '@ecommerce/core/services/delivery'
import type { Carrier, DeliveryOption } from '@ecommerce/core/services/delivery/types'

/** The carrier, for linking tracking references. It changes rarely; a page need not ask again for a while. */
export function useCarrier() {
  return useQuery({ queryKey: queryKeys.carrier(), queryFn: () => Delivery.carrier(), staleTime: 10 * 60_000, retry: false })
}

export function useDeliverySettings() {
  return useQuery({ queryKey: queryKeys.deliverySettings(), queryFn: () => Delivery.settings() })
}

/** Saving an option or the carrier refreshes the settings, the carrier and what checkout offers. */
export function useDeliveryChanges() {
  const queryClient = useQueryClient()
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.deliverySettings() }),
      queryClient.invalidateQueries({ queryKey: queryKeys.carrier() }),
      queryClient.invalidateQueries({ queryKey: queryKeys.shippingOptions() }),
    ])
  return {
    option: useMutation({ mutationFn: (option: DeliveryOption) => Delivery.saveOption(option), onSuccess: refresh }),
    carrier: useMutation({ mutationFn: (carrier: Carrier) => Delivery.saveCarrier(carrier), onSuccess: refresh }),
  }
}
