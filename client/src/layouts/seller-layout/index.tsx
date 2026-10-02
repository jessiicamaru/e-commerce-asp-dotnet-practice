import { useTranslation } from 'react-i18next'
import { Link, NavLink, Outlet } from 'react-router-dom'
import {
  ChartColumnIcon,
  LayoutDashboardIcon,
  MessageCircleQuestionIcon,
  PackageIcon,
  PlusIcon,
  ReceiptTextIcon,
  StoreIcon,
  TicketPercentIcon,
  Undo2Icon,
  WalletIcon,
} from 'lucide-react'
import { DescribeShopDialog } from '@/components/seller/describe-shop-dialog'
import { RenameShopDialog } from '@/components/seller/rename-shop-dialog'
import { buttonVariants } from '@/components/ui/button'
import { useAuth } from '@/context/auth/useAuth'
import { useSellerWaiting } from '@/hooks/order'
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
  // What waits behind Sales, Questions and Returns (specs/131) - the home's panel reads the same counts.
  const waiting = useSellerWaiting(isSeller)

  const links = [
    { to: '/shop', end: true, icon: LayoutDashboardIcon, label: t('menu.overview') },
    { to: '/shop/insights', end: false, icon: ChartColumnIcon, label: t('menu.insights') },
    { to: '/shop/products', end: false, icon: PackageIcon, label: t('menu.products') },
    { to: '/shop/sales', end: false, icon: ReceiptTextIcon, label: t('menu.sales'), count: waiting.toPrepare },
    { to: '/shop/returns', end: false, icon: Undo2Icon, label: t('menu.returns'), count: waiting.returns },
    { to: '/shop/questions', end: false, icon: MessageCircleQuestionIcon, label: t('menu.questions'), count: waiting.questions },
    { to: '/shop/payouts', end: false, icon: WalletIcon, label: t('menu.payouts') },
    { to: '/shop/vouchers', end: false, icon: TicketPercentIcon, label: t('menu.vouchers') },
  ]

  return (
    <div className="grid gap-6 lg:grid-cols-[15rem_1fr]">
      {/* Each grid here has one column that may shrink (minmax(0,1fr)): an `auto` column is as wide as its content's
          min-content, and a truncated shop name's min-content is the whole name - so a long name widened the card past
          the 15rem column and the sidebar covered the page (specs/117, #238). */}
      <aside data-testid="seller-sidebar" className="grid min-w-0 grid-cols-[minmax(0,1fr)] content-start gap-4 lg:sticky lg:top-28 lg:self-start">
        <div className="bg-card ring-border/60 grid grid-cols-[minmax(0,1fr)] gap-3 rounded-3xl p-4 ring-1">
          <div className="flex items-center gap-3">
            <span className="bg-primary text-primary-foreground grid size-10 shrink-0 place-items-center rounded-2xl">
              <StoreIcon className="size-5" />
            </span>
            <div className="min-w-0">
              <p className="text-muted-foreground text-xs">{t('title')}</p>
              <p className="truncate font-semibold" title={shop.data?.shopName}>
                {shop.data?.shopName ?? '…'}
              </p>
            </div>
          </div>
          {shop.data && (
            <div className="flex flex-wrap gap-2">
              <RenameShopDialog current={shop.data.shopName} />
              <DescribeShopDialog current={shop.data.description ?? ''} />
            </div>
          )}
          {shop.data && (
            <Link to={`/shops/${shop.data.sellerId}`} className="text-muted-foreground hover:text-foreground text-xs underline-offset-4 hover:underline">
              {t('viewShop')}
            </Link>
          )}
        </div>

        {/* Four equal columns on a phone, icon over label, so every destination is on screen - a row
            that scrolled sideways hid the fourth, "Payouts", past the edge at 390px. */}
        <nav className="bg-card ring-border/60 grid grid-cols-4 gap-1 rounded-3xl p-2 ring-1 lg:grid-cols-1">
          {links.map(({ to, end, icon: Icon, label, count }: { to: string; end: boolean; icon: typeof PackageIcon; label: string; count?: number }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn(
                  'relative flex flex-col items-center gap-1 rounded-2xl px-1 py-2 text-center text-xs font-medium transition-colors lg:flex-row lg:gap-3 lg:px-3 lg:py-2.5 lg:text-left lg:text-sm',
                  isActive ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
                )
              }
            >
              <Icon className="size-4.5" /> <span className="lg:flex-1">{label}</span>
              {!!count && (
                <span
                  className="bg-foreground text-background absolute top-1 right-1 rounded-full px-1.5 text-[10px] leading-4 font-semibold tabular-nums lg:static lg:px-2 lg:py-0.5 lg:text-xs"
                  aria-label={t('menuWaiting', { count })}
                >
                  {count}
                </span>
              )}
            </NavLink>
          ))}
        </nav>

        <Link to="/shop/products/new" className={cn(buttonVariants(), 'h-10 rounded-full font-semibold')}>
          <PlusIcon /> {t('listing.new')}
        </Link>
      </aside>

      <div className="min-w-0" data-testid="seller-page">
        <Outlet />
      </div>
    </div>
  )
}
