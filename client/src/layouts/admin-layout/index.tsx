import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router-dom'
import { GavelIcon, MessageSquareIcon, PackageCheckIcon, ScrollTextIcon, ShieldCheckIcon, StoreIcon, TruckIcon, UsersIcon, WalletIcon } from 'lucide-react'
import { useAuth } from '@/context/auth/useAuth'
import { cn } from '@/utils/shared'

/**
 * The frame every administrator page sits in (specs/038) - the seller console's shape, so the two
 * places people run things from feel like one product: a sidebar that never moves, tabs on a phone.
 *
 * <p>
 * Drawn for an administrator or a moderator (specs/043), and that is courtesy: every request behind these pages is refused
 * by the server for anybody else.
 * </p>
 */
export function AdminLayout() {
  const { t } = useTranslation('admin')
  const { isAdmin } = useAuth()

  // Each person sees the pages their role can use (specs/043): a moderator has no fulfilment, payouts or
  // audit log to open, and a link to a page that answers 403 is a link to an error.
  const links = [
    { to: '/admin', end: true, icon: TruckIcon, label: t('menu.fulfilment'), adminOnly: true },
    { to: '/admin/payouts', end: false, icon: WalletIcon, label: t('menu.payouts'), adminOnly: true },
    { to: '/admin/moderation', end: false, icon: GavelIcon, label: t('menu.moderation'), adminOnly: false },
    { to: '/admin/products', end: false, icon: PackageCheckIcon, label: t('menu.review'), adminOnly: false },
    { to: '/admin/shops', end: false, icon: StoreIcon, label: t('menu.shops'), adminOnly: false },
    { to: '/admin/reviews', end: false, icon: MessageSquareIcon, label: t('menu.reviews'), adminOnly: false },
    { to: '/admin/users', end: false, icon: UsersIcon, label: t('menu.users'), adminOnly: false },
    { to: '/admin/audit', end: false, icon: ScrollTextIcon, label: t('menu.audit'), adminOnly: true },
  ].filter((link) => isAdmin || !link.adminOnly)

  return (
    <div className="grid gap-6 lg:grid-cols-[15rem_1fr]">
      <aside className="grid content-start gap-4 lg:sticky lg:top-28 lg:self-start">
        <div className="bg-card ring-border/60 flex items-center gap-3 rounded-3xl p-4 ring-1">
          <span className="bg-primary text-primary-foreground grid size-10 shrink-0 place-items-center rounded-2xl">
            <ShieldCheckIcon className="size-5" />
          </span>
          <p className="font-semibold">{t('title')}</p>
        </div>

        <nav className="bg-card ring-border/60 flex gap-1 overflow-x-auto rounded-3xl p-2 ring-1 lg:grid">
          {links.map(({ to, end, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn(
                  'flex shrink-0 items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium transition-colors',
                  isActive ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
                )
              }
            >
              <Icon className="size-4.5" /> {label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="min-w-0">
        <Outlet />
      </div>
    </div>
  )
}
