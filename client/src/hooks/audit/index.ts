import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Audit } from '@/services/audit'
import type { AuditFilter } from '@/services/audit/types'

/** A page of the log. Keeps the previous page on screen while the next loads. */
export function useAuditLog(filter: AuditFilter, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.auditLog(filter, page),
    queryFn: () => Audit.list(filter, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/** One entry, with its diff - loaded when it is opened, not with the list. */
export function useAuditEntry(id: string | null) {
  return useQuery({
    queryKey: queryKeys.auditEntry(id ?? ''),
    queryFn: () => Audit.get(id!),
    enabled: id !== null,
    retry: false,
  })
}

/** How many entries each category holds since `from` - the tabs' numbers. */
export function useAuditSummary(from?: string) {
  return useQuery({ queryKey: queryKeys.auditSummary(from ?? ''), queryFn: () => Audit.summary(from) })
}
