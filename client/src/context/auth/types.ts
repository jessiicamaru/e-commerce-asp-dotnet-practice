import type { SignUpInput, User } from '@/services/auth/types'

export interface AuthState {
  user: User | null
  /** True until the first silent refresh on load has answered. */
  restoring: boolean
  signIn(email: string, password: string): Promise<void>
  signUp(input: SignUpInput): Promise<void>
  signOut(): Promise<void>
}
