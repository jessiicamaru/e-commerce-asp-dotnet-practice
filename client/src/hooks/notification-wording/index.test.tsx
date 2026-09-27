import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { queryKeys } from '@/constants/query-keys'
import { NotificationWording } from '@/services/notification-wording'
import { applyWording } from '@/utils/notifications/wording'
import { useNotificationWording } from '.'

let client: QueryClient

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

const newSale = () => i18n.t('notifications:kind.NewSale', { order: '01a0dd2b' })

beforeEach(async () => {
  client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  await i18n.changeLanguage('en')
})

// The wording is laid over a shared i18n instance: put the bundle back for the next test.
afterEach(() => applyWording(i18n, {}))

describe('useNotificationWording (specs/078)', () => {
  /**
   * #186 (specs/094): "Activity down leaves the bundled words" held by construction only. A notice with no words is a
   * blank line in the bell - the failure this hook's `data`-only effect exists to avoid.
   */
  it('keeps the bundled words when the wording cannot be fetched', async () => {
    const bundled = newSale()
    vi.spyOn(NotificationWording, 'current').mockRejectedValue(new Error('503 Activity is down'))

    renderHook(() => useNotificationWording(), { wrapper })

    // Settled, and rendered after it: whatever the hook does with a failure has been done.
    await waitFor(() => expect(client.getQueryState(queryKeys.notificationWording())?.status).toBe('error'))
    await waitFor(() => expect(newSale()).toBe(bundled))
    expect(newSale()).toContain('01a0dd2b')
  })

  /** The control: the same hook does lay an edit over the bundle, so the test above is not passing on a no-op. */
  it('lays an administrator’s edit over the bundled words when it can', async () => {
    vi.spyOn(NotificationWording, 'current').mockResolvedValue({ en: { NewSale: 'Sold at last: {{order}}' } })

    renderHook(() => useNotificationWording(), { wrapper })

    await waitFor(() => expect(newSale()).toBe('Sold at last: 01a0dd2b'))
  })
})
