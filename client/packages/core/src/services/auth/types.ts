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
  /**
   * Whether the address was confirmed by its link (specs/063) - for drawing the "confirm your email" banner.
   * The server decides what an unconfirmed account may do; a missing value (an older Identity) reads true.
   */
  emailConfirmed: boolean
  /** Staff without two-factor sign-in: their staff pages wait until they set it up (specs/110). */
  twoFactorSetupRequired?: boolean
  /**
   * The account is staff, whatever this session carries (specs/138): a storefront session never holds a staff role, so
   * this is how the storefront knows to offer the back office. For drawing only; a missing value (an older Identity)
   * reads false.
   */
  staffAccount?: boolean
}

export interface AuthResponse extends User {
  /** Short-lived, kept in memory only. The refresh token is an HttpOnly cookie this never sees. */
  token: string
  /**
   * `'Required'`: the right password, and now the code - no session yet, `token` is empty and `challenge` is what the
   * second step exchanges. `'SetupRequired'`: staff without two-factor sign-in, signed in WITHOUT their staff roles until
   * they set it up (specs/110). For drawing; the server decides.
   */
  twoFactor?: TwoFactorState | null
  challenge?: string | null
}

export type TwoFactorState = 'Required' | 'SetupRequired'

/** The second step: the code from the authenticator app, or one recovery code. */
export type SecondFactor = { code: string } | { recoveryCode: string }

/** The signed-in person's own two-factor sign-in (specs/110). */
export interface TwoFactorStatus {
  enabled: boolean
  enabledAt: string | null
  recoveryCodesLeft: number
  /** Staff: it cannot be turned off. */
  required: boolean
}

/** Shown ONCE: the secret as base32, for typing, and the `otpauth://` URI the QR code draws. */
export interface TwoFactorSetup {
  secret: string
  uri: string
}

/** The signed-in person's own details (specs/064). The email is shown, not changed, here. */
export interface AccountProfile {
  email: string
  firstName: string
  lastName: string
  phone: string | null
  emailConfirmed: boolean
}

export interface ProfileInput {
  firstName: string
  lastName: string
  phone: string
}

export interface SignUpInput {
  email: string
  password: string
  firstName: string
  lastName: string
}
