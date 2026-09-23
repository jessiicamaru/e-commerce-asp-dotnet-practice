import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Reviews } from '@/services/review'

export function useProductReviews(productId: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.productReviews(productId, page),
    queryFn: () => Reviews.forProduct(productId, page, pageSize),
    placeholderData: (previous) => previous,
    enabled: productId !== '',
  })
}

/** Asked only when signed in: a visitor has no review and cannot be eligible. */
export function useMyReview(productId: string, signedIn: boolean) {
  return useQuery({
    queryKey: queryKeys.myReview(productId),
    queryFn: () => Reviews.mine(productId),
    enabled: signedIn && productId !== '',
  })
}

/**
 * Writes the review, then re-reads the reviews AND the product - its average and count changed on the
 * server, in the same transaction, and the page shows the server's numbers rather than guessing.
 */
export function useWriteReview(productId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ rating, body }: { rating: number; body: string }) => Reviews.write(productId, rating, body),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['reviews', productId] })
      await queryClient.invalidateQueries({ queryKey: ['product', productId] })
    },
  })
}

export function useStaffReviews(hidden: boolean, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.staffReviews(hidden, page),
    queryFn: () => Reviews.forStaff(hidden, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

export function useReviewModeration() {
  const queryClient = useQueryClient()
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['staff-reviews'] })
    await queryClient.invalidateQueries({ queryKey: ['reviews'] })
    await queryClient.invalidateQueries({ queryKey: ['my-decisions'] })
  }
  return {
    hide: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) => Reviews.hide(id, reason),
      onSettled: refresh,
    }),
    restore: useMutation({ mutationFn: (id: string) => Reviews.restore(id), onSettled: refresh }),
  }
}
