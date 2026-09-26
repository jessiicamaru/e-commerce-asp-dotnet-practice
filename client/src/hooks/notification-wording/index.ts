import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { queryKeys } from '@/constants/query-keys'
import { NotificationWording } from '@/services/notification-wording'
import { applyWording } from '@/utils/notifications/wording'

/** How often the storefront asks for new wording - an edit reaches an open page within this, a new page at once. */
export const WORDING_REFRESH_MS = 5 * 60_000

/**
 * The notices' current words, laid over the bundled ones (specs/078). Called once, high in the tree. Unreachable
 * Activity means the bundled words - never blank notices.
 */
export function useNotificationWording() {
  const { i18n } = useTranslation()
  const wording = useQuery({
    queryKey: queryKeys.notificationWording(),
    queryFn: () => NotificationWording.current(),
    staleTime: WORDING_REFRESH_MS,
    refetchInterval: WORDING_REFRESH_MS,
    retry: false,
  })

  useEffect(() => {
    if (wording.data) applyWording(i18n, wording.data)
  }, [i18n, wording.data])
}

export function useWordingOverview() {
  return useQuery({ queryKey: queryKeys.wordingOverview(), queryFn: () => NotificationWording.overview() })
}

export function useWordingVersions(key: string, language: string) {
  return useQuery({
    queryKey: queryKeys.wordingVersions(key, language),
    queryFn: () => NotificationWording.versions(key, language),
  })
}

/** Save, reset and restore for one key in one language, each on top of `expectedVersion`; then everything is re-read. */
export function useWordingChanges(key: string, language: string, expectedVersion: number) {
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['notification-wording'] })
  return {
    save: useMutation({
      mutationFn: (text: string) => NotificationWording.save(key, language, text, expectedVersion),
      onSuccess: refresh,
    }),
    reset: useMutation({ mutationFn: () => NotificationWording.reset(key, language, expectedVersion), onSuccess: refresh }),
    restore: useMutation({
      mutationFn: (version: number) => NotificationWording.restore(key, language, version, expectedVersion),
      onSuccess: refresh,
    }),
  }
}
