import { useTranslation } from 'react-i18next'
import { Link, NavLink, Outlet } from 'react-router-dom'
import { LayoutDashboardIcon, PackageIcon, PlusIcon, ReceiptTextIcon, StoreIcon, WalletIcon } from 'lucide-react'
import { RenameShopDialog } from '@/components/seller/rename-shop-dialog'
import { buttonVariants } from '@/components/ui/button'
import { useAuth } from '@/context/auth/useAuth'
import { useMyShop } from '@/hooks/seller'
import { cn } from '@/utils/shared'

/**
 * The frame every seller page sits in: which shop this is, where you can go, and the one action a
 * seller takes most - listing something.
 *
 * <p>
 * A sidebar rather than links scattered across each page, because this is a place somebody manages a
 * business from, and the thing that makes a tool feel like one is that the way around it never moves.
 * On a phone the same destinations become a row of tabs at the top.
 * </p>
 * <p>
 * Nothing here names a seller in an address. Every request behind these pages carries a token and the
 * server answers with that seller's shop (specs/027).
 * </p>
 */
export function SellerLayout() {
  const { t } = useTranslation('seller')
  const { isSeller } = useAuth()
  const shop = useMyShop(isSeller)

  const links = [
    { to: '/shop', end: true, icon: LayoutDashboardIcon, label: t('menu.overview') },
    { to: '/shop/products', end: false, icon: PackageIcon, label: t('menu.products') },
    { to: '/shop/sales', end: false, icon: ReceiptTextIcon, label: t('menu.sales') },
    { to: '/shop/payouts', end: false, icon: WalletIcon, label: t('menu.payouts') },
  ]

  return (
    <div className="grid gap-6 lg:grid-cols-[15rem_1fr]">
      <aside className="grid content-start gap-4 lg:sticky lg:top-28 lg:self-start">
        <div className="bg-card ring-border/60 grid gap-3 rounded-3xl p-4 ring-1">
          <div className="flex items-center gap-3">
            <span className="bg-primary text-primary-foreground grid size-10 shrink-0 place-items-center rounded-2xl">
              <StoreIcon className="size-5" />
            </span>
            <div className="min-w-0">
              <p className="text-muted-foreground text-xs">{t('title')}</p>
              <p className="truncate font-semibold">{shop.data?.shopName ?? '…'}</p>
            </div>
          </div>
          {shop.data && <RenameShopDialog current={shop.data.shopName} />}
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

        <Link to="/shop/products/new" className={cn(buttonVariants(), 'h-10 rounded-full font-semibold')}>
          <PlusIcon /> {t('listing.new')}
        </Link>
      </aside>

      <div className="min-w-0">
        <Outlet />
      </div>
    </div>
  )
}
