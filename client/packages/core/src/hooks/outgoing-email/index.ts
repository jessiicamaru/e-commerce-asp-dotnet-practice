import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { OutgoingEmails } from '@ecommerce/core/services/outgoing-email'
import type { OutgoingEmailStatus } from '@ecommerce/core/services/outgoing-email/types'

/** The emails in one state, a page at a time (specs/087). Keeps the previous page on screen while the next loads. */
export function useOutgoingEmails(status: OutgoingEmailStatus, search: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.outgoingEmails(status, search, page, pageSize),
    queryFn: () => OutgoingEmails.list(status, search, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/** Sends a failed email again, then re-reads every list - it has moved from one to another. */
export function useRetryEmail() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => OutgoingEmails.retry(id),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['outgoing-emails'] }),
  })
}
