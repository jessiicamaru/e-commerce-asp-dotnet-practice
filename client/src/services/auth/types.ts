export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
}

export interface AuthResponse extends User {
  /** Short-lived, kept in memory only. The refresh token is an HttpOnly cookie this never sees. */
  token: string
}

export interface SignUpInput {
  email: string
  password: string
  firstName: string
  lastName: string
}
