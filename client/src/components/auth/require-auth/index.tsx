import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '@/context/auth/useAuth'

/** Pages that need a signed-in customer. Waits for the silent refresh on load before deciding. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { user, restoring } = useAuth()
  const location = useLocation()

  if (restoring) {
    return <p className="text-muted-foreground">{t('checkingSession')}</p>
  }

  if (!user) {
    return <Navigate to="/sign-in" replace state={{ from: location.pathname }} />
  }

  return <>{children}</>
}
