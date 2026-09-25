import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Auth } from '@/services/auth'
import type { ProfileInput } from '@/services/auth/types'

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
