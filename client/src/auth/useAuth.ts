import { createContext, useContext } from 'react'

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
}

export interface AuthResponse extends User {
  token: string
}

export interface AuthState {
  user: User | null
  /** True until the first silent refresh on load has answered. */
  restoring: boolean
  signIn(email: string, password: string): Promise<void>
  signUp(input: { email: string; password: string; firstName: string; lastName: string }): Promise<void>
  signOut(): Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}
