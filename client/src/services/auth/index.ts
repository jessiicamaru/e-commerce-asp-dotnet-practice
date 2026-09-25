import { http } from '@/config/axios'
import type { AuthResponse, SignUpInput } from './types'

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
