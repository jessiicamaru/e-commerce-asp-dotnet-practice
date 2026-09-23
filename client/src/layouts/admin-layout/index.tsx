import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router-dom'
import { ShieldCheckIcon, TruckIcon, WalletIcon } from 'lucide-react'
import { cn } from '@/utils/shared'

/**
 * The frame every administrator page sits in (specs/038) - the seller console's shape, so the two
 * places people run things from feel like one product: a sidebar that never moves, tabs on a phone.
 *
 * <p>
 * Drawn only for an administrator, and that is courtesy: every request behind these pages is refused
 * by the server for anybody else.
 * </p>
 */
export function AdminLayout() {
  const { t } = useTranslation('admin')

  const links = [
    { to: '/admin', end: true, icon: TruckIcon, label: t('menu.fulfilment') },
    { to: '/admin/payouts', end: false, icon: WalletIcon, label: t('menu.payouts') },
  ]

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
