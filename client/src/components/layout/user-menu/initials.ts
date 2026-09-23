import type { User } from '@/services/auth/types'

/** Two letters for the avatar: "Mai Trần" → "MT". Falls back to the email when there is no name. */
export function initialsOf(user: Pick<User, 'firstName' | 'lastName' | 'email'>): string {
  const letters = [user.firstName, user.lastName]
    .map((part) => part?.trim().charAt(0) ?? '')
    .join('')
  return (letters || user.email.charAt(0)).toUpperCase()
}
