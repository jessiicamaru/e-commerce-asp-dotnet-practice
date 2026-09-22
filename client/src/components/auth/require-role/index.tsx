import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '@/context/auth/useAuth'

/**
 * Pages only a seller is offered (specs/028).
 *
 * <p>
 * <b>This is not a permission.</b> It decides what to draw, and anybody who edits their own
 * JavaScript can draw whatever they like - what they cannot do is make the server accept it. Every
 * write behind these pages is refused by the controller attributes and by `SellerOwnership`, which
 * answers 404 for somebody else's product. If a rule in this feature exists only here, it does not
 * exist.
 * </p>
 * <p>
 * Signed out goes to sign-in, remembering where they were. Signed in but not a seller goes to the
 * catalogue rather than to an error: a customer who typed /shop made a wrong turn, and telling them
 * "forbidden" would also tell them the page is real.
 * </p>
 */
export function RequireRole({ role, children }: { role: string; children: ReactNode }) {
  const { t } = useTranslation()
  const { user, restoring } = useAuth()
  const location = useLocation()

  if (restoring) {
    return <p className="text-muted-foreground">{t('checkingSession')}</p>
  }

  if (!user) {
    return <Navigate to="/sign-in" replace state={{ from: location.pathname }} />
  }

  if (!user.roles.includes(role)) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}
