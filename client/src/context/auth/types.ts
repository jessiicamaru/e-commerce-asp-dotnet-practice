import type { SignUpInput, User } from '@/services/auth/types'

export interface AuthState {
  user: User | null
  /** True until the first silent refresh on load has answered. */
  restoring: boolean
  /**
   * Whether to offer this person a shop (specs/028). False while `restoring`, so nothing flashes
   * a shop link at somebody whose session has not come back yet.
   */
  isSeller: boolean
  signIn(email: string, password: string): Promise<void>
  signUp(input: SignUpInput): Promise<void>
  signOut(): Promise<void>
}
