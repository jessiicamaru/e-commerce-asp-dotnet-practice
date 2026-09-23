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
  /**
   * Whether to offer the administrator's console (specs/038). For DRAWING only, like `isSeller`: every
   * request behind the console is refused by the server for anybody else.
   */
  isAdmin: boolean
  /**
   * Admin or Moderator (specs/043): whether to offer the console at all. What each sees inside it is
   * decided per page; what each may DO is decided by the server.
   */
  isStaff: boolean
  /**
   * Asks Identity for a fresh session now (specs/044) - how roles granted since sign-in, like Seller after
   * an approval, reach this tab without signing out. False when the session could not be renewed.
   */
  refreshSession(): Promise<boolean>
  signIn(email: string, password: string): Promise<void>
  signUp(input: SignUpInput): Promise<void>
  signOut(): Promise<void>
}
