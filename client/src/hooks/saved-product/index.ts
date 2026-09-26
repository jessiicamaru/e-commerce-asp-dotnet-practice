import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { SavedProduct } from '@/services/saved-product'

/**
 * The ids the shopper saved - one request, shared by every heart on the page. `enabled` keeps a signed-out
 * visitor from asking and collecting a 401.
 */
export function useSavedIds(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.savedIds(),
    queryFn: () => SavedProduct.ids(),
    enabled,
    staleTime: 60_000,
    select: (ids) => new Set(ids),
  })
}

/** Saves or unsaves, then re-reads the ids and the list - what they now hold is the server's to say. */
export function useToggleSaved() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ productId, saved }: { productId: string; saved: boolean }) =>
      saved ? SavedProduct.unsave(productId) : SavedProduct.save(productId),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['saved'] }),
  })
}

export function useSavedProducts(page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.savedProducts(page),
    queryFn: () => SavedProduct.list(page, pageSize),
    placeholderData: (previous) => previous,
  })
}
