import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '@/config/axios'

/**
 * One TanStack Query client for the app.
 *
 * Retries are deliberately narrow: a 4xx is an answer, not a hiccup, and retrying it only delays the
 * message a person needs to read. The axios layer already retries a 401 once, after refreshing.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (failureCount, error) => {
        const status = error instanceof ApiError ? error.status : 0
        if (status >= 400 && status < 500) {
          return false
        }
        return failureCount < 2
      },
    },
  },
})
