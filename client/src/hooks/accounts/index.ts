import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Accounts } from '@/services/accounts'

/** A page of people matching the search. Keeps the previous page on screen while the next loads. */
export function useAccounts(search: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.accounts(search, page),
    queryFn: () => Accounts.search(search, page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/**
 * What staff do to an account (specs/043). Each re-reads the list rather than patching it: the server
 * decides what the account now holds.
 */
export function useAccountActions() {
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['accounts'] })

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
  }
}
