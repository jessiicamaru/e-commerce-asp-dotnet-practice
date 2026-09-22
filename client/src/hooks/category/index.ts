import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Category } from '@/services/category'

export function useCategories() {
  return useQuery({
    queryKey: queryKeys.categories(),
    queryFn: () => Category.list(),
    // Categories change rarely; this keeps the filter from refetching on every visit.
    staleTime: 5 * 60_000,
  })
}
