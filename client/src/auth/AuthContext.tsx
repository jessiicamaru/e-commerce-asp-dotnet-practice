import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { api, configureAuth } from '../api/http'
import { AuthContext, type AuthResponse, type AuthState, type User } from './useAuth'

// Who is signed in, and the access token - held in MEMORY ONLY (feature 015, #35).
//
// Never localStorage or sessionStorage: anything a script can read, an injected script can steal. The
// refresh token is Identity's HttpOnly cookie, which no script can read at all; on a reload the
// storefront asks for a fresh access token with it, so a person stays signed in without the token
// ever being stored anywhere readable. See docs/features/auth/security-best-practices.md.

// Module-level, so the HTTP layer can read it synchronously on every request.
let accessToken: string | null = null

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [restoring, setRestoring] = useState(true)
  const refreshing = useRef<Promise<boolean> | null>(null)

  const accept = useCallback((r: AuthResponse) => {
    accessToken = r.token
    setUser({ id: r.id, email: r.email, firstName: r.firstName, lastName: r.lastName })
  }, [])

  // One refresh at a time: several requests failing with 401 together share it.
  const refresh = useCallback((): Promise<boolean> => {
    refreshing.current ??= api<AuthResponse>('/auth/refresh', { method: 'POST', anonymous: true })
      .then((r) => {
        accept(r)
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
        accept(await api<AuthResponse>('/auth/login', { method: 'POST', body: { email, password }, anonymous: true }))
      },
      async signUp(input) {
        accept(await api<AuthResponse>('/auth/register', { method: 'POST', body: input, anonymous: true }))
      },
      async signOut() {
        // Server first: the cookie must stop opening sessions, or the next reload signs straight back in.
        try {
          await api<void>('/auth/logout', { method: 'POST', anonymous: true })
        } finally {
          accessToken = null
          setUser(null)
        }
      },
    }),
    [user, restoring, accept],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
