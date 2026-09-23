import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Moderation } from '@/services/moderation'
import type { ReviewStatus } from '@/services/product/types'

/** A page of one review status. Keeps the previous page on screen while the next loads. */
export function useReviewQueue(status: ReviewStatus, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.reviewQueue(status, page),
    queryFn: () => Moderation.products(status, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

export function useMyDecisions(pageSize: number) {
  return useQuery({ queryKey: queryKeys.myDecisions, queryFn: () => Moderation.myDecisions(1, pageSize) })
}

/**
 * Approve, reject, take down. Every queue and the "recently" list are re-read afterwards, whatever
 * happened: the product has moved, or somebody else moved it first.
 */
export function useReviewDecision() {
  const queryClient = useQueryClient()
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['review-queue'] })
    await queryClient.invalidateQueries({ queryKey: queryKeys.myDecisions })
  }

  return {
    approve: useMutation({ mutationFn: (id: string) => Moderation.approve(id), onSettled: refresh }),
    reject: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) => Moderation.reject(id, reason),
      onSettled: refresh,
    }),
    takeDown: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) => Moderation.takeDown(id, reason),
      onSettled: refresh,
    }),
  }
}
