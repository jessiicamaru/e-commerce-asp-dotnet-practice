export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  /**
   * What the server says this person holds (specs/028) - used to decide what to **draw**, never
   * what to allow. Every refusal that matters is the server's; hiding a button is not a check.
   *
   * Order is not guaranteed. Ask with `includes`, never by position.
   */
  roles: string[]
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
