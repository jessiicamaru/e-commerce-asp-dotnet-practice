import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useLocation } from 'react-router-dom'
import { Button } from '@ecommerce/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@ecommerce/ui/card'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'

/**
 * Who the back office lets in (specs/136): staff - an administrator or a moderator - signed in with their code. For
 * DRAWING only, like every role check in a client: each request behind it is decided by the server on its own.
 *
 * - Nobody signed in: to sign in, and back here afterwards.
 * - Staff who have not set up two-factor sign-in: the server gave them no staff role (specs/110), so they are told
 *   where to set it up - their account on the storefront - rather than shown an empty console.
 * - Anybody else: this is for staff; nothing but signing out.
 */
export function RequireStaff({ children }: { children: ReactNode }) {
  const { t } = useTranslation('backOffice')
  const { user, restoring, isStaff, signOut } = useAuth()
  const location = useLocation()

  if (restoring) {
    return <p className="text-muted-foreground p-8">{t('checking')}</p>
  }

  if (!user) {
    return <Navigate to="/sign-in" replace state={{ from: location.pathname }} />
  }

  if (isStaff) {
    return <>{children}</>
  }

  const unenrolled = user.twoFactorSetupRequired
  return (
    <main className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-xl">{unenrolled ? t('setupRequired.title') : t('notStaff.title')}</CardTitle>
          <CardDescription>{unenrolled ? t('setupRequired.body') : t('notStaff.body', { email: user.email })}</CardDescription>
        </CardHeader>
        <CardContent>
          <Button variant="outline" className="rounded-full" onClick={() => void signOut()}>
            {t('signOut')}
          </Button>
        </CardContent>
      </Card>
    </main>
  )
}
