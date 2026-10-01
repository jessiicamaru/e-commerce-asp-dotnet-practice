import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Accounts } from '@/services/accounts'

/** A page of people matching the search. Keeps the previous page on screen while the next loads. */
export function useAccounts(search: string, page: number, pageSize: number, includeDeleted = false) {
  return useQuery({
    queryKey: queryKeys.accounts(search, page, includeDeleted),
    queryFn: () => Accounts.search(search, page, pageSize, includeDeleted),
    placeholderData: (previous) => previous,
  })
}

/** What staff decided about one person before (specs/100). */
export function usePersonHistory(id: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.personHistory(id, page, pageSize),
    queryFn: () => Accounts.history(id, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/**
 * What staff do to an account (specs/043). Each re-reads the list rather than patching it: the server
 * decides what the account now holds.
 */
export function useAccountActions() {
  const queryClient = useQueryClient()
  // A decision is also a new line in the person's history (specs/100).
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['accounts'] }),
      queryClient.invalidateQueries({ queryKey: ['person-history'] }),
    ])

  return {
    grant: useMutation({ mutationFn: (id: string) => Accounts.grantModerator(id), onSuccess: refresh }),
    revoke: useMutation({ mutationFn: (id: string) => Accounts.revokeModerator(id), onSuccess: refresh }),
    lock: useMutation({
      mutationFn: ({ id, days, reason }: { id: string; days: number; reason: string }) => Accounts.lock(id, days, reason),
      onSuccess: refresh,
    }),
    unlock: useMutation({ mutationFn: (id: string) => Accounts.unlock(id), onSuccess: refresh }),
    ban: useMutation({ mutationFn: ({ id, reason }: { id: string; reason: string }) => Accounts.ban(id, reason), onSuccess: refresh }),
    liftBan: useMutation({ mutationFn: (id: string) => Accounts.liftBan(id), onSuccess: refresh }),
    resetTwoFactor: useMutation({ mutationFn: (id: string) => Accounts.resetTwoFactor(id), onSuccess: refresh }),
  }
}
