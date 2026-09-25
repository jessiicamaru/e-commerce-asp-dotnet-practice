import { http } from '@/config/axios'
import type { AccountProfile, AuthResponse, ProfileInput, SignUpInput } from './types'

/**
 * Identity's session endpoints. Anonymous on purpose: none of them may carry the access token or
 * trigger a refresh - the refresh call itself would recurse.
 */
export class Auth {
  static async signIn(email: string, password: string): Promise<AuthResponse> {
    const { data } = await http.post<AuthResponse>('/auth/login', { email, password }, { anonymous: true })
    return data
  }

  static async signUp(input: SignUpInput): Promise<AuthResponse> {
    const { data } = await http.post<AuthResponse>('/auth/register', input, { anonymous: true })
    return data
  }

  /**
   * Asks for a link to choose a new password (specs/061). Succeeds the same way for any address - the
   * server never says whether it has an account (#28). The email is written in the request's language.
   */
  static async forgotPassword(email: string): Promise<void> {
    await http.post('/auth/forgot-password', { email }, { anonymous: true })
  }

  /** Chooses a new password with the token from the link; every session of the account ends. */
  static async resetPassword(token: string, password: string): Promise<void> {
    await http.post('/auth/reset-password', { token, password }, { anonymous: true })
  }

  /** Uses the link sent to confirm an address (specs/063). Anonymous: the link may be opened in any browser. */
  static async confirmEmail(token: string): Promise<void> {
    await http.post('/auth/confirm-email', { token }, { anonymous: true })
  }

  /** Sends the signed-in person a new confirmation link (specs/063); at most one a minute is really sent. */
  static async resendConfirmation(): Promise<void> {
    await http.post('/auth/resend-confirmation')
  }

  /** The signed-in person's own details (specs/064). */
  static async me(): Promise<AccountProfile> {
    const { data } = await http.get<AccountProfile>('/auth/me')
    return data
  }

  /** Changes the signed-in person's name and phone (specs/064). */
  static async updateMe(input: ProfileInput): Promise<AccountProfile> {
    const { data } = await http.put<AccountProfile>('/auth/me', input)
    return data
  }

  /**
   * Changes the signed-in person's password (specs/064). This browser stays signed in - Identity keeps the
   * session its HttpOnly cookie names - and every other one ends.
   */
  static async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    await http.put('/auth/me/password', { currentPassword, newPassword })
  }

  /** Trades the HttpOnly refresh cookie for a new pair. A replayed token ends every session (#29). */
  static async refresh(): Promise<AuthResponse> {
    const { data } = await http.post<AuthResponse>('/auth/refresh', undefined, { anonymous: true })
    return data
  }

  /** Ends the session on the server; the cookie is what a client cannot delete by itself (#35). */
  static async signOut(): Promise<void> {
    await http.post('/auth/logout', undefined, { anonymous: true })
  }
}
