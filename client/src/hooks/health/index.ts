import { useQueries } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { HEALTH_SERVICES, Health } from '@/services/health'

/** Every service's health at once, in the order they are listed. */
export function useHealth() {
  return useQueries({
    queries: HEALTH_SERVICES.map((service) => ({
      queryKey: [...queryKeys.health(), service],
      queryFn: () => Health.check(service),
      retry: false,
      staleTime: 0,
    })),
    combine: (results) =>
      HEALTH_SERVICES.map((service, index) => ({
        service,
        pending: results[index].isPending,
        health: results[index].data,
      })),
  })
}
