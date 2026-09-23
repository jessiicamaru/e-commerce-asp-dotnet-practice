import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { NOTIFICATION_POLL_MS } from '@/constants/notifications'
import { Notifications } from '@/services/notifications'

/**
 * The bell's number, asked again every 30 seconds (specs/042, decided with the user: polling, not a
 * socket). Only while the tab is visible - a background tab asking twice a minute all afternoon is load
 * for nobody.
 */
export function useUnreadCount(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.unreadCount(),
    queryFn: () => Notifications.unreadCount(),
    enabled,
    refetchInterval: NOTIFICATION_POLL_MS,
    refetchIntervalInBackground: false,
  })
}

/** A page of the inbox. `enabled` lets the bell load it only when opened. */
export function useNotifications(page: number, pageSize: number, unreadOnly = false, enabled = true) {
  return useQuery({
    queryKey: queryKeys.notifications(page, unreadOnly),
    queryFn: () => Notifications.list(page, pageSize, unreadOnly),
    enabled,
    placeholderData: (previous) => previous,
  })
}

/** Marking read re-reads the count and the lists: the server says what is unread, not the page. */
export function useMarkRead() {
  const queryClient = useQueryClient()
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.unreadCount() })
    await queryClient.invalidateQueries({ queryKey: ['notifications'] })
  }

  return {
    one: useMutation({ mutationFn: (id: string) => Notifications.markRead(id), onSuccess: refresh }),
    all: useMutation({ mutationFn: () => Notifications.markAllRead(), onSuccess: refresh }),
  }
}
