import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router-dom'
import { SignInForm } from '@ecommerce/core/components/sign-in-form'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { Auth } from '@ecommerce/core/services/auth'

/**
 * One redemption per code, whatever renders this page how often: a handoff works once (specs/140), and React's
 * development mode runs an effect twice - the second would spend nothing but would be refused, and read as a failure.
 */
const redemptions = new Map<string, Promise<string | null>>()

function redeem(code: string): Promise<string | null> {
  if (!redemptions.has(code)) {
    redemptions.set(code, Auth.redeemHandoff(code).then((answer) => answer.challenge ?? null, () => null))
  }
  return redemptions.get(code)!
}

/**
 * Where the storefront's "Management platform" lands (#279, specs/140): `/auth/callback#code=...`. The code leaves the
 * address at once. A back office already signed in goes straight to the console; otherwise the code is redeemed for a
 * challenge and the sign-in form opens at the code step. Anything else - no code, a used or expired one - is the
 * ordinary sign-in: a failed handoff costs a password, never a way in.
 */
export function AuthCallbackPage() {
  const { t } = useTranslation('backOffice')
  const { user, restoring, isStaff } = useAuth()
  const navigate = useNavigate()
  const [code] = useState(() => new URLSearchParams(window.location.hash.slice(1)).get('code'))
  const [challenge, setChallenge] = useState<string | null | undefined>(undefined)

  // The code out of the address before anything else: not in the history, not in a bookmark, not on screen.
  useEffect(() => {
    if (window.location.hash) window.history.replaceState(window.history.state, '', window.location.pathname)
  }, [])

  useEffect(() => {
    if (restoring || (user && isStaff) || !code) return
    let current = true
    void redeem(code).then((answer) => {
      if (current) setChallenge(answer)
    })
    return () => {
      current = false
    }
  }, [code, restoring, user, isStaff])

  if (restoring) return <p className="text-muted-foreground p-8">{t('checking')}</p>
  if (user && isStaff) return <Navigate to="/" replace />
  if (!code || challenge === null) return <Navigate to="/sign-in" replace />
  if (challenge === undefined) return <p className="text-muted-foreground p-8">{t('checking')}</p>

  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-6 p-4">
      <p className="font-heading text-lg font-semibold">{t('name')}</p>
      <div className="w-full">
        <SignInForm
          initialChallenge={challenge}
          onSignedIn={() => navigate('/', { replace: true })}
          onSetupRequired={() => navigate('/', { replace: true })}
        />
      </div>
    </main>
  )
}
