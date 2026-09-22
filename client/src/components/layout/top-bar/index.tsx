import { useTranslation } from 'react-i18next'
import { NavLink } from 'react-router-dom'
import { LanguageSwitcher } from '@/components/layout/language-switcher'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/context/auth/useAuth'
import { cn } from '@/utils/shared'

const link = ({ isActive }: { isActive: boolean }) =>
  cn('text-sm hover:underline', isActive && 'font-semibold')

export function TopBar() {
  const { t } = useTranslation()
  const { user, restoring, signOut } = useAuth()

  return (
    <header className="bg-background/80 sticky top-0 z-10 border-b backdrop-blur">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3">
        <NavLink to="/" className="text-base font-bold">
          {t('brand')}
        </NavLink>
        <nav className="flex flex-wrap items-center gap-4">
          <LanguageSwitcher />
          <NavLink to="/status" className={link}>
            {t('nav.status')}
          </NavLink>
          {restoring ? null : user ? (
            <>
              <NavLink to="/cart" className={link}>
                {t('nav.cart')}
              </NavLink>
              <NavLink to="/orders" end className={link}>
                {t('nav.orders')}
              </NavLink>
              <NavLink to="/account" className={link}>
                {user.firstName}
              </NavLink>
              <Button variant="ghost" size="sm" onClick={() => void signOut()}>
                {t('nav.signOut')}
              </Button>
            </>
          ) : (
            <>
              <NavLink to="/sign-in" className={link}>
                {t('nav.signIn')}
              </NavLink>
              <NavLink to="/sign-up" className={link}>
                {t('nav.signUp')}
              </NavLink>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
