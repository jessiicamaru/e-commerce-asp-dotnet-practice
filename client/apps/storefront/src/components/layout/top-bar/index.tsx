import { useTranslation } from 'react-i18next'
import { backOfficeUrl } from '@ecommerce/core/config/apps'
import { Link, NavLink, useNavigate, useSearchParams } from 'react-router-dom'
import {
  ApertureIcon,
  LogInIcon,
  MenuIcon,
  PackageIcon,
  SearchIcon,
  ShoppingBagIcon,
  ShieldCheckIcon,
  StoreIcon,
  UserIcon,
} from 'lucide-react'
import { CurrencySwitcher } from '@/components/layout/currency-switcher'
import { LanguageSwitcher } from '@/components/layout/language-switcher'
import { NotificationBell } from '@/components/layout/notification-bell'
import { UserMenu } from '@/components/layout/user-menu'
import { Button, buttonVariants } from '@ecommerce/ui/button'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@ecommerce/ui/input-group'
import { Separator } from '@ecommerce/ui/separator'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@ecommerce/ui/sheet'
import { Tooltip, TooltipContent, TooltipTrigger } from '@ecommerce/ui/tooltip'
import { accountDestinations, destinationClick } from '@ecommerce/core/constants/account'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { useCartCount } from '@ecommerce/core/hooks/cart'
import { cn } from 'cn'

/**
 * The bar every page hangs from.
 *
 * <p>
 * <b>Icons carry the navigation, words carry the meaning.</b> The cart, the orders and the shop are
 * icons with tooltips and accessible names; everything about the person - their account, addresses,
 * orders, shop and signing out - is behind their initials. Five words across the bar read as a list
 * of links; this reads as a shop.
 * </p>
 * <p>
 * The search lives here rather than on the catalogue alone, because a shopper looking at one camera
 * and wanting another should not have to go back first. It writes the same `?q=` the catalogue reads.
 * </p>
 */
export function TopBar() {
  const { t } = useTranslation()
  const { user, restoring, isSeller, hasBackOffice, signOut } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()

  // The account's cart, or this browser's when signed out (specs/162).
  const lines = useCartCount()

  function search(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const term = new FormData(event.currentTarget).get('q')?.toString().trim() ?? ''
    navigate(term ? `/?q=${encodeURIComponent(term)}` : '/')
  }

  return (
    <header className="sticky top-0 z-30 px-4 pt-4">
      <div className="bg-card/85 ring-border/60 mx-auto flex max-w-6xl flex-wrap items-center gap-2 rounded-3xl py-2 pr-2 pl-3 shadow-sm ring-1 backdrop-blur-md md:flex-nowrap md:rounded-full">
        <Link to="/" className="flex shrink-0 items-center gap-2 pr-2" aria-label={t('brand')}>
          <span className="bg-primary text-primary-foreground grid size-9 place-items-center rounded-full">
            <ApertureIcon className="size-5" />
          </span>
          <span className="hidden text-base font-bold tracking-tight sm:inline">{t('brand')}</span>
        </Link>

        <form onSubmit={search} className="order-last w-full md:order-0 md:flex-1">
          <InputGroup className="bg-secondary/70 h-10 rounded-full border-0">
            <InputGroupAddon>
              <SearchIcon />
            </InputGroupAddon>
            <InputGroupInput
              name="q"
              type="search"
              defaultValue={params.get('q') ?? ''}
              placeholder={t('searchPlaceholder')}
              aria-label={t('action.search')}
            />
          </InputGroup>
        </form>

        <div className="ml-auto flex items-center gap-1">
          <div className="hidden items-center gap-1 md:flex">
            <LanguageSwitcher />
            <CurrencySwitcher />
          </div>

          {restoring ? null : user ? (
            <>
              <IconLink to="/cart" label={t('nav.cart')} badge={lines}>
                <ShoppingBagIcon />
              </IconLink>
              <NotificationBell />
              <span className="hidden md:contents">
                <IconLink to="/orders" label={t('nav.orders')} end>
                  <PackageIcon />
                </IconLink>
                {isSeller && (
                  <IconLink to="/shop" label={t('seller:nav')}>
                    <StoreIcon />
                  </IconLink>
                )}
                {hasBackOffice && (
                  <IconLink to={backOfficeUrl()} label={t('admin:goToBackOffice')} onClick={destinationClick(backOfficeUrl())}>
                    <ShieldCheckIcon />
                  </IconLink>
                )}
              </span>
              <span className="hidden md:inline-flex md:pl-1">
                <UserMenu user={user} isSeller={isSeller} isStaff={hasBackOffice} onSignOut={() => void signOut()} />
              </span>
            </>
          ) : (
            <span className="flex items-center gap-1">
              {/* This browser's cart (specs/162): shown once it holds something. */}
              {lines > 0 && (
                <IconLink to="/cart" label={t('nav.cart')} badge={lines}>
                  <ShoppingBagIcon />
                </IconLink>
              )}
            <span className="hidden items-center gap-1 md:flex">
              <NavLink to="/sign-in" className={cn(buttonVariants({ variant: 'ghost' }), 'h-9 rounded-full px-4')}>
                <LogInIcon /> {t('nav.signIn')}
              </NavLink>
              {/* A styled link, not a Button wrapping one: base-ui's Button has no `asChild`, and a
                  button that navigates should be an anchor anyway. */}
              <NavLink to="/sign-up" className={cn(buttonVariants(), 'h-9 rounded-full px-4 font-semibold')}>
                {t('nav.signUp')}
              </NavLink>
            </span>
            </span>
          )}

          <MobileMenu />
        </div>
      </div>
    </header>
  )
}

/** An icon that navigates, named for screen readers and explained by a tooltip for everybody else. */
function IconLink({
  to,
  label,
  badge = 0,
  end,
  onClick,
  children,
}: {
  to: string
  label: string
  badge?: number
  end?: boolean
  onClick?: (event: { preventDefault(): void }) => void
  children: React.ReactNode
}) {
  return (
    <Tooltip>
      <TooltipTrigger
        render={
          <NavLink
            to={to}
            end={end}
            onClick={onClick}
            aria-label={badge > 0 ? `${label} (${badge})` : label}
            className={({ isActive }) =>
              cn(
                buttonVariants({ variant: 'ghost', size: 'icon-lg' }),
                'relative rounded-full [&_svg]:size-5',
                isActive && 'bg-secondary',
              )
            }
          />
        }
      >
        {children}
        {badge > 0 && (
          <span className="bg-primary text-primary-foreground ring-card absolute -top-0.5 -right-0.5 grid h-4.5 min-w-4.5 place-items-center rounded-full px-1 text-[10px] leading-none font-bold ring-2">
            {badge}
          </span>
        )}
      </TooltipTrigger>
      <TooltipContent>{label}</TooltipContent>
    </Tooltip>
  )
}

/**
 * Everything that does not fit across a phone, in a sheet. It is the same destinations as the bar -
 * not a second navigation with its own ideas - plus the language and currency, which on a phone have
 * no room anywhere else.
 */
function MobileMenu() {
  const { t } = useTranslation()
  const { user, isSeller, hasBackOffice, signOut } = useAuth()

  const item = ({ isActive }: { isActive: boolean }) =>
    cn(
      'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors [&_svg]:size-4.5',
      isActive ? 'bg-secondary' : 'hover:bg-secondary/70',
    )

  return (
    <Sheet>
      <SheetTrigger
        render={<Button variant="ghost" size="icon-lg" className="rounded-full md:hidden" aria-label={t('nav.menu')} />}
      >
        <MenuIcon className="size-5" />
      </SheetTrigger>
      <SheetContent side="right" className="w-80 gap-0 p-0">
        <SheetHeader className="border-b p-4">
          <SheetTitle className="flex items-center gap-2">
            <ApertureIcon className="size-5" /> {t('brand')}
          </SheetTitle>
        </SheetHeader>

        <nav className="grid gap-1 p-3">
          {user ? (
            <>
              <p className="text-muted-foreground px-3 pb-1 text-xs">
                {user.firstName} {user.lastName} · {user.email}
              </p>
              {/* The one list every account menu draws (specs/127): it was missing Saved, Notifications and Open a shop. */}
              {accountDestinations({ isSeller, isStaff: hasBackOffice }).map(({ to, icon: Icon, label }) => (
                <NavLink key={to} to={to} end className={item} onClick={destinationClick(to)}>
                  <Icon /> {t(label)}
                </NavLink>
              ))}
            </>
          ) : (
            <>
              <NavLink to="/sign-in" className={item}>
                <LogInIcon /> {t('nav.signIn')}
              </NavLink>
              <NavLink to="/sign-up" className={item}>
                <UserIcon /> {t('nav.signUp')}
              </NavLink>
            </>
          )}
        </nav>

        <Separator />
        <div className="flex items-center gap-1 p-3">
          <LanguageSwitcher />
          <CurrencySwitcher />
        </div>

        {user && (
          <>
            <Separator />
            <div className="p-3">
              <Button variant="ghost" className="text-destructive w-full justify-start rounded-xl" onClick={() => void signOut()}>
                {t('nav.signOut')}
              </Button>
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  )
}
