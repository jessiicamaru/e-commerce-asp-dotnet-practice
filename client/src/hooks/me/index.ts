import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Auth } from '@/services/auth'
import type { ProfileInput } from '@/services/auth/types'
import { MyData } from '@/services/my-data'
import { MY_DATA_SERVICES, type MyDataService, type ServiceExport } from '@/services/my-data/types'
import { composeMyData } from '@/utils/account'

/** The signed-in person's own details (specs/064). */
export function useMe() {
  return useQuery({ queryKey: queryKeys.me(), queryFn: () => Auth.me() })
}

/** Changes the signed-in person's name and phone; the answer is the new details. */
export function useUpdateMe() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: ProfileInput) => Auth.updateMe(input),
    onSuccess: (profile) => queryClient.setQueryData(queryKeys.me(), profile),
  })
}

/** Changes the signed-in person's password. Every other session ends; this one stays. */
export function useChangePassword() {
  return useMutation({
    mutationFn: ({ currentPassword, newPassword }: { currentPassword: string; newPassword: string }) =>
      Auth.changePassword(currentPassword, newPassword),
  })
}

/** The signed-in person's own two-factor sign-in (specs/110). */
export function useMyTwoFactor() {
  return useQuery({ queryKey: queryKeys.myTwoFactor(), queryFn: () => Auth.twoFactor() })
}

/**
 * Setting up, confirming, new recovery codes and turning off. Confirming renews the session afterwards: the one that
 * confirmed is now verified, and only a renewed token carries staff roles.
 */
export function useTwoFactorMoves(renewSession: () => Promise<boolean>) {
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: queryKeys.myTwoFactor() })

  return {
    setUp: useMutation({ mutationFn: () => Auth.setUpTwoFactor() }),
    confirm: useMutation({
      mutationFn: (code: string) => Auth.confirmTwoFactor(code),
      onSuccess: async () => {
        await renewSession()
        await refresh()
      },
    }),
    newCodes: useMutation({ mutationFn: (code: string) => Auth.newRecoveryCodes(code), onSuccess: refresh }),
    turnOff: useMutation({
      mutationFn: ({ password, code }: { password: string; code: string }) => Auth.turnOffTwoFactor(password, code),
      onSuccess: refresh,
    }),
  }
}

/**
 * Everything the shop holds about the signed-in person (specs/111): the six services asked at once, and one file
 * composed from whatever answered - a service that did not is marked in the file and named in the answer.
 */
export function useDownloadMyData() {
  return useMutation({
    mutationFn: async (person: { id: string; email: string }) => {
      const settled = await Promise.allSettled(MY_DATA_SERVICES.map((service) => MyData.of(service)))
      const answers = Object.fromEntries(MY_DATA_SERVICES.map((service, i) => [service, settled[i]])) as Record<
        MyDataService,
        PromiseSettledResult<ServiceExport>
      >
      return composeMyData(person, answers, new Date())
    },
  })
}
