import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { configureAuth } from '@/config/axios'
import { Auth } from '@/services/auth'
import type { AuthResponse, SignUpInput, User } from '@/services/auth/types'
import { AuthContext } from './useAuth'
import type { AuthState } from './types'

// Who is signed in, and the access token - held in MEMORY ONLY (feature 015, #35).
//
// Never localStorage or sessionStorage: anything a script can read, an injected script can steal. The
// refresh token is Identity's HttpOnly cookie, which no script can read at all; on a reload the
// storefront asks for a fresh access token with it, so a person stays signed in without the token
// ever being stored anywhere readable. See docs/features/auth/security-best-practices.md.

// Module-level, so the axios request interceptor can read it synchronously on every request.
let accessToken: string | null = null

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<User | null>(null)
  const [restoring, setRestoring] = useState(true)
  const refreshing = useRef<Promise<boolean> | null>(null)

  const accept = useCallback((response: AuthResponse) => {
    accessToken = response.token
    setUser({
      id: response.id,
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
    })
  }, [])

  // One refresh at a time: several requests failing with 401 together share it.
  const refresh = useCallback((): Promise<boolean> => {
    refreshing.current ??= Auth.refresh()
      .then((response) => {
        accept(response)
        return true
      })
      .catch(() => {
        accessToken = null
        setUser(null)
        return false
      })
      .finally(() => {
        refreshing.current = null
      })

    return refreshing.current
  }, [accept])

  useEffect(() => {
    configureAuth(() => accessToken, refresh)
    // A reload loses the in-memory token; the refresh cookie brings the session back.
    void refresh().finally(() => setRestoring(false))
  }, [refresh])

  const value = useMemo<AuthState>(
    () => ({
      user,
      restoring,
      async signIn(email, password) {
        accept(await Auth.signIn(email, password))
        // Whoever was signed in before, their cart and orders are not this person's.
        await queryClient.invalidateQueries()
      },
      async signUp(input: SignUpInput) {
        accept(await Auth.signUp(input))
        await queryClient.invalidateQueries()
      },
      async signOut() {
        // Server first: the cookie must stop opening sessions, or the next reload signs straight back in.
        try {
          await Auth.signOut()
        } finally {
          accessToken = null
          setUser(null)
          queryClient.clear()
        }
      },
    }),
    [user, restoring, accept, queryClient],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
