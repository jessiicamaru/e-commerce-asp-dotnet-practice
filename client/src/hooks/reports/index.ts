import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Moderation } from '@/services/moderation'
import { Questions } from '@/services/question'
import { Reports } from '@/services/reports'
import type { NewReport, ReportedItem } from '@/services/reports/types'
import { Reviews } from '@/services/review'

/** A shopper reports something (specs/101). */
export function useReport() {
  return useMutation({ mutationFn: (report: NewReport) => Reports.create(report) })
}

export function useReportQueue(page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.reportQueue(page),
    queryFn: () => Reports.queue(page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/**
 * What staff do about one reported thing: the existing hide or take-down (which closes the reports on the server), or
 * dismiss. Each re-reads the queue and whatever else shows the thing.
 */
export function useReportActions(item: ReportedItem) {
  const queryClient = useQueryClient()
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['reports'] })
    await queryClient.invalidateQueries({ queryKey: ['my-decisions'] })
    await queryClient.invalidateQueries({ queryKey: item.targetType === 'Review' ? ['staff-reviews'] : ['questions'] })
  }

  return {
    act: useMutation({
      mutationFn: (reason: string): Promise<unknown> =>
        item.targetType === 'Review'
          ? Reviews.hide(item.targetId, reason)
          : item.targetType === 'Question'
            ? Questions.hide(item.targetId, reason)
            : Moderation.takeDown(item.targetId, reason),
      onSettled: refresh,
    }),
    dismiss: useMutation({ mutationFn: () => Reports.dismiss(item.targetType, item.targetId), onSettled: refresh }),
  }
}
