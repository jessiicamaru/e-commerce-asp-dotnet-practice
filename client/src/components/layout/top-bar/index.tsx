import { useTranslation } from 'react-i18next'
import { NavLink, useNavigate, useSearchParams } from 'react-router-dom'
import { CurrencySwitcher } from '@/components/layout/currency-switcher'
import { LanguageSwitcher } from '@/components/layout/language-switcher'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useAuth } from '@/context/auth/useAuth'
import { useCart } from '@/hooks/cart'
import { cn } from '@/utils/shared'

const link = ({ isActive }: { isActive: boolean }) =>
  cn(
    'rounded-full px-3 py-1.5 text-sm transition-colors',
    isActive ? 'bg-secondary font-semibold' : 'hover:bg-secondary/70',
  )

/**
 * The bar every page hangs from: the name, a search box, and who you are.
 *
 * <p>
 * The search lives here rather than on the catalogue page alone, because a shopper looking at one
 * camera and wanting a different one should not have to go back first. It writes the same `?q=` the
 * catalogue reads, so a search is still a shareable address.
 * </p>
 */
export function TopBar() {
  const { t } = useTranslation()
  const { user, restoring, signOut } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()

  // Only a signed-in shopper has a cart; asking for one while signed out is a guaranteed 401.
  const cart = useCart(!!user)
  const lines = cart.data?.lines.length ?? 0

  function search(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const term = new FormData(event.currentTarget).get('q')?.toString().trim() ?? ''
    navigate(term ? `/?q=${encodeURIComponent(term)}` : '/')
  }

  return (
    <header className="sticky top-0 z-20 px-4 pt-4">
      {/* A pill only while it is one row. Wrapped onto four rows at 500px it became a tall
            stadium, which is what a full radius does to a tall box - seen in a screenshot, not
            guessed. */}
      <div className="bg-card/85 ring-border/60 mx-auto flex max-w-6xl flex-wrap items-center gap-3 rounded-3xl py-2.5 pr-2.5 pl-5 shadow-sm ring-1 backdrop-blur-md sm:rounded-full">
        <NavLink to="/" className="text-base font-bold tracking-tight">
          {t('brand')}
        </NavLink>

        <form onSubmit={search} className="order-last w-full sm:order-0 sm:w-auto sm:flex-1">
          <Input
            name="q"
            type="search"
            defaultValue={params.get('q') ?? ''}
            placeholder={t('searchPlaceholder')}
            aria-label={t('action.search')}
            className="bg-secondary/70 h-9 rounded-full border-0 px-4"
          />
        </form>

        <nav className="flex flex-wrap items-center gap-1">
          <LanguageSwitcher />
          <CurrencySwitcher />

          {restoring ? null : user ? (
            <>
              <NavLink to="/cart" className={link}>
                {t('nav.cart')}
                {lines > 0 && (
                  <span className="bg-primary text-primary-foreground ml-1.5 rounded-full px-1.5 py-0.5 text-xs font-semibold">
                    {lines}
                  </span>
                )}
              </NavLink>
              <NavLink to="/orders" end className={link}>
                {t('nav.orders')}
              </NavLink>
              <NavLink to="/account" className={link}>
                {user.firstName}
              </NavLink>
              <Button variant="ghost" size="sm" className="rounded-full" onClick={() => void signOut()}>
                {t('nav.signOut')}
              </Button>
            </>
          ) : (
            <>
              <NavLink to="/sign-in" className={link}>
                {t('nav.signIn')}
              </NavLink>
              {/* A styled link, not a Button wrapping one: this Button is base-ui's and has no
                  `asChild`, and a button that navigates should be an anchor anyway. */}
              <NavLink
                to="/sign-up"
                className="bg-primary text-primary-foreground rounded-full px-4 py-1.5 text-sm font-semibold transition-opacity hover:opacity-90"
              >
                {t('nav.signUp')}
              </NavLink>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
