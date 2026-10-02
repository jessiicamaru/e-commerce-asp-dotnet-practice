import { LogOutIcon, ShieldIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Outlet } from 'react-router-dom'
import { Button } from '@ecommerce/ui/button'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'

/** The frame of every back-office page (specs/136): its name, who is signed in, and signing out. The console's
 *  navigation arrives with its pages (#277). */
export function BackOfficeLayout() {
  const { t } = useTranslation('backOffice')
  const { user, signOut } = useAuth()

  return (
    <div className="min-h-svh">
      <header className="bg-card ring-border/60 mx-auto mt-4 flex max-w-6xl items-center gap-3 rounded-full px-5 py-3 ring-1">
        <ShieldIcon className="text-primary size-5" />
        <span className="font-heading font-semibold">{t('name')}</span>
        <span className="text-muted-foreground ml-auto truncate text-sm">{user?.email}</span>
        <Button variant="ghost" size="sm" className="rounded-full" onClick={() => void signOut()}>
          <LogOutIcon /> {t('signOut')}
        </Button>
      </header>
      <main className="mx-auto max-w-6xl p-4 sm:p-6">
        <Outlet />
      </main>
    </div>
  )
}
