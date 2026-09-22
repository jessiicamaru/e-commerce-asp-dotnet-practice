import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Product } from '@/services/product'
import type { ProductQuery } from '@/services/product/types'

export function useProducts(query: ProductQuery) {
  return useQuery({
    queryKey: queryKeys.products(query),
    queryFn: () => Product.list(query),
    // The previous page stays on screen while the next one loads, instead of flashing empty.
    placeholderData: (previous) => previous,
  })
}

export function useProduct(id: string) {
  return useQuery({
    queryKey: queryKeys.product(id),
    queryFn: () => Product.get(id),
    enabled: id !== '',
    // A product that does not exist will not start existing on a retry.
    retry: false,
  })
}
