import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { BellIcon, FlagIcon, FolderTreeIcon, GavelIcon, LayoutDashboardIcon, MailIcon, MailWarningIcon, MessageCircleQuestionIcon, MessageSquareIcon, PackageCheckIcon, PackageIcon, ScrollTextIcon, SearchIcon, ShieldCheckIcon, StoreIcon, TicketPercentIcon, TruckIcon, Undo2Icon, UsersIcon, WalletIcon, type LucideIcon } from 'lucide-react'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { useStaffWaiting } from '@ecommerce/core/hooks/admin'
import { cn } from 'cn'

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
  const location = useLocation()
  const waiting = useStaffWaiting({ isAdmin })

  // An order belongs to the list it was opened from (specs/129): that link stays lit, else Orders to ship.
  const from = (location.state as { from?: string } | null)?.from?.split('?')[0]
  const openOrder = /^\/admin\/orders\/(?!find)/.test(location.pathname)
  const owner = openOrder ? (from && from.startsWith('/admin') ? from : '/admin') : null

  // Grouped (specs/129, #246) - it was 19 links in one column. Each person sees the pages their role can use (specs/043):
  // a moderator has no fulfilment, payouts or audit log to open, and a link to a page that answers 403 is a link to an error.
  const groups: { heading?: string; links: Link[] }[] = [
    { links: [{ to: '/admin/overview', end: false, icon: LayoutDashboardIcon, label: t('menu.overview'), adminOnly: true }] },
    {
      heading: t('menu.group.orders'),
      links: [
        { to: '/admin', end: true, icon: TruckIcon, label: t('menu.fulfilment'), adminOnly: true, count: waiting.fulfilment },
        { to: '/admin/orders/find', end: false, icon: SearchIcon, label: t('menu.findOrder'), adminOnly: true },
        { to: '/admin/returns', end: false, icon: Undo2Icon, label: t('menu.returns'), adminOnly: true, count: waiting.returns },
        { to: '/admin/delivery', end: false, icon: PackageIcon, label: t('menu.delivery'), adminOnly: true },
      ],
    },
    {
      heading: t('menu.group.money'),
      links: [
        { to: '/admin/payouts', end: false, icon: WalletIcon, label: t('menu.payouts'), adminOnly: true },
        { to: '/admin/vouchers', end: false, icon: TicketPercentIcon, label: t('menu.vouchers'), adminOnly: true },
      ],
    },
    {
      heading: t('menu.group.catalogue'),
      links: [{ to: '/admin/categories', end: false, icon: FolderTreeIcon, label: t('menu.categories'), adminOnly: true }],
    },
    {
      heading: t('menu.group.moderation'),
      links: [
        { to: '/admin/moderation', end: false, icon: GavelIcon, label: t('menu.moderation'), adminOnly: false },
        { to: '/admin/products', end: false, icon: PackageCheckIcon, label: t('menu.review'), adminOnly: false, count: waiting.products },
        { to: '/admin/shops', end: false, icon: StoreIcon, label: t('menu.shops'), adminOnly: false, count: waiting.shops },
        { to: '/admin/reviews', end: false, icon: MessageSquareIcon, label: t('menu.reviews'), adminOnly: false },
        { to: '/admin/questions', end: false, icon: MessageCircleQuestionIcon, label: t('menu.questions'), adminOnly: false },
        { to: '/admin/reports', end: false, icon: FlagIcon, label: t('menu.reports'), adminOnly: false, count: waiting.reports },
        { to: '/admin/users', end: false, icon: UsersIcon, label: t('menu.users'), adminOnly: false },
      ],
    },
    {
      heading: t('menu.group.messages'),
      links: [
        { to: '/admin/emails', end: false, icon: MailIcon, label: t('menu.emails'), adminOnly: true },
        { to: '/admin/email-delivery', end: false, icon: MailWarningIcon, label: t('menu.emailDelivery'), adminOnly: true },
        { to: '/admin/notifications', end: false, icon: BellIcon, label: t('menu.notifications'), adminOnly: true },
      ],
    },
    {
      heading: t('menu.group.system'),
      links: [{ to: '/admin/audit', end: false, icon: ScrollTextIcon, label: t('menu.audit'), adminOnly: true }],
    },
  ]
    .map((group) => ({ ...group, links: group.links.filter((link) => isAdmin || !link.adminOnly) }))
    .filter((group) => group.links.length > 0)

  return (
    <div className="grid gap-6 lg:grid-cols-[15rem_1fr]">
      <aside className="grid min-w-0 grid-cols-[minmax(0,1fr)] content-start gap-4 lg:sticky lg:top-28 lg:self-start">
        <div className="bg-card ring-border/60 flex items-center gap-3 rounded-3xl p-4 ring-1">
          <span className="bg-primary text-primary-foreground grid size-10 shrink-0 place-items-center rounded-2xl">
            <ShieldCheckIcon className="size-5" />
          </span>
          <p className="font-semibold">{t('title')}</p>
        </div>

        <nav className="bg-card ring-border/60 flex gap-1 overflow-x-auto rounded-3xl p-2 ring-1 lg:grid">
          {groups.map((group, index) => (
            <div key={group.heading ?? index} role="group" aria-label={group.heading} className="contents lg:grid lg:gap-1">
              {group.heading && (
                <p className="text-muted-foreground hidden px-3 pt-2 text-xs font-semibold tracking-wide uppercase lg:block">
                  {group.heading}
                </p>
              )}
              {group.links.map(({ to, end, icon: Icon, label, count }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    cn(
                      'flex shrink-0 items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium transition-colors',
                      (owner === null ? isActive : owner === to)
                        ? 'bg-primary text-primary-foreground'
                        : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
                    )
                  }
                >
                  <Icon className="size-4.5" /> <span className="flex-1">{label}</span>
                  {!!count && (
                    <span
                      className="bg-foreground text-background rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums"
                      aria-label={t('menu.waiting', { count })}
                    >
                      {count}
                    </span>
                  )}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
      </aside>

      <div className="min-w-0">
        <Outlet />
      </div>
    </div>
  )
}

interface Link {
  to: string
  end: boolean
  icon: LucideIcon
  label: string
  adminOnly: boolean
  /** What is waiting there (specs/129); nothing drawn for none. */
  count?: number
}
