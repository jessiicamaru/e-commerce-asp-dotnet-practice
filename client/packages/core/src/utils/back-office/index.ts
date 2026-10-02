import { backOfficeUrl } from '@ecommerce/core/config/apps'
import { Auth } from '@ecommerce/core/services/auth'

/**
 * Crosses from the storefront to the back office (#279, specs/140): with a single-use handoff when one can be had, so
 * the back office asks only for the code from the authenticator, and with the bare address when not - a failed
 * handoff costs a password, never a way in. The code travels in the FRAGMENT, which no server sees: not nginx's log,
 * not the gateway's, not a Referer.
 */
export async function goToBackOffice(): Promise<void> {
  let code: string | null = null
  try {
    code = await Auth.handoff()
  } catch {
    code = null
  }
  window.location.assign(code ? backOfficeUrl(`/auth/callback#code=${encodeURIComponent(code)}`) : backOfficeUrl())
}
