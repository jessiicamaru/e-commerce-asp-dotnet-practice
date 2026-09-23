import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { AlertTriangleIcon, ArrowRightIcon, PackageIcon, ReceiptTextIcon, WalletIcon } from 'lucide-react'
import { ProductImage } from '@/components/product/product-image'
import { PageTitle } from '@/components/seller/page-title'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuth } from '@/context/auth/useAuth'
import { useMySales } from '@/hooks/order'
import { useMyProducts } from '@/hooks/product'
import { describeSaleStatus } from '@/pages/shop-sales/status'
import { summariseSales } from '@/utils/seller'
import { cn, money } from '@/utils/shared'

/** How far back the overview looks. The sales endpoint pages at 100, and the page says when there is more. */
const OVERVIEW_WINDOW = 100

/**
 * A seller's shop at a glance (specs/028, 034): how much is listed, what has run out, what has sold
 * and for how much - and the two lists a seller acts on, the latest sales and what needs restocking.
 *
 * <p>
 * <b>Revenue is shown per currency</b> and is the seller's own lines only: an order paid in dollars and
 * one paid in dong do not add up, and the shop converts nothing (specs/022).
 * </p>
 */
export function ShopPage() {
  const { t, i18n } = useTranslation('seller')
  const { isSeller } = useAuth()

  const listings = useMyProducts({ pageNumber: 1, pageSize: OVERVIEW_WINDOW }, isSeller)
  const sales = useMySales(1, OVERVIEW_WINDOW, isSeller)

  if (listings.isError || sales.isError) {
    return <ErrorMessage>{t('listing.loadFailed')}</ErrorMessage>
  }

  if (listings.isPending || sales.isPending || !listings.data || !sales.data) {
    return <LoadingRows rows={4} />
  }

  const products = listings.data.items
  const soldOut = products.filter((product) => product.availability !== 'InStock')
  const summary = summariseSales(sales.data.items)
  const partial = sales.data.totalCount > sales.data.items.length

  if (listings.data.totalCount === 0) {
    return (
      <section className="grid gap-6">
        <PageTitle title={t('menu.overview')} />
        <div className="bg-card ring-border/60 grid justify-items-start gap-3 rounded-3xl p-8 ring-1">
          <h2 className="text-lg font-semibold">{t('empty.title')}</h2>
          <p className="text-muted-foreground max-w-prose text-sm">{t('empty.body')}</p>
          <Link to="/shop/products/new" className={cn(buttonVariants(), 'rounded-full px-4 font-semibold')}>
            {t('empty.action')}
          </Link>
        </div>
      </section>
    )
  }

  return (
    <section className="grid gap-6">
      <PageTitle title={t('menu.overview')} subtitle={t('overview.subtitle')} />

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Stat icon={PackageIcon} label={t('overview.listings')} value={String(listings.data.totalCount)} />
        <Stat
          icon={AlertTriangleIcon}
          label={t('overview.soldOut')}
          value={String(soldOut.length)}
          tone={soldOut.length > 0 ? 'warn' : undefined}
        />
        <Stat icon={ReceiptTextIcon} label={t('overview.orders')} value={String(sales.data.totalCount)} />
        <Stat
          icon={WalletIcon}
          label={t('overview.revenue')}
          value={
            Object.keys(summary.revenue).length === 0
              ? money(0, 'VND')
              : Object.entries(summary.revenue)
                  .map(([currency, amount]) => money(amount, currency))
                  .join(' · ')
          }
          hint={partial ? t('overview.revenueWindow', { count: OVERVIEW_WINDOW }) : t('overview.revenueHint')}
        />
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card className="rounded-3xl">
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>{t('overview.recentSales')}</CardTitle>
            <Link to="/shop/sales" className="text-muted-foreground hover:text-foreground flex items-center gap-1 text-sm">
              {t('overview.all')} <ArrowRightIcon className="size-4" />
            </Link>
          </CardHeader>
          <CardContent className="grid gap-1">
            {sales.data.items.length === 0 ? (
              <p className="text-muted-foreground text-sm">{t('sales.none')}</p>
            ) : (
              sales.data.items.slice(0, 5).map((sale) => (
                <Link
                  key={sale.orderId}
                  to={`/shop/sales/${sale.orderId}`}
                  className="hover:bg-secondary/70 flex items-center justify-between gap-3 rounded-xl px-3 py-2.5"
                >
                  <span className="grid min-w-0">
                    <span className="truncate text-sm font-medium">
                      {new Date(sale.createdAt).toLocaleString(i18n.language)}
                    </span>
                    <span className="text-muted-foreground text-xs">
                      {t('sales.units', { count: sale.units })} · {describeSaleStatus(t, sale.status)}
                    </span>
                  </span>
                  <span className="shrink-0 text-sm font-semibold">{money(sale.subtotal, sale.currency)}</span>
                </Link>
              ))
            )}
          </CardContent>
        </Card>

        <Card className="rounded-3xl">
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>{t('overview.restock')}</CardTitle>
            <Link to="/shop/products" className="text-muted-foreground hover:text-foreground flex items-center gap-1 text-sm">
              {t('overview.all')} <ArrowRightIcon className="size-4" />
            </Link>
          </CardHeader>
          <CardContent className="grid gap-1">
            {soldOut.length === 0 ? (
              <p className="text-muted-foreground text-sm">{t('overview.nothingSoldOut')}</p>
            ) : (
              soldOut.slice(0, 5).map((product) => (
                <Link
                  key={product.id}
                  to={`/shop/products/${product.id}`}
                  className="hover:bg-secondary/70 flex items-center gap-3 rounded-xl px-3 py-2"
                >
                  <span className="w-12 shrink-0">
                    <ProductImage product={product} thumb />
                  </span>
                  <span className="grid min-w-0 flex-1">
                    <span className="truncate text-sm font-medium">{product.name}</span>
                    <span className="text-muted-foreground text-xs">{product.sku}</span>
                  </span>
                  <Badge variant="outline" className="text-destructive border-destructive/40">
                    {t('listing.outOfStock')}
                  </Badge>
                </Link>
              ))
            )}
          </CardContent>
        </Card>
      </div>
    </section>
  )
}

function Stat({
  icon: Icon,
  label,
  value,
  hint,
  tone,
}: {
  icon: React.ComponentType<{ className?: string }>
  label: string
  value: string
  hint?: string
  tone?: 'warn'
}) {
  return (
    <Card className="rounded-3xl">
      <CardContent className="grid gap-3">
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground text-sm">{label}</span>
          <span
            className={cn(
              'grid size-9 place-items-center rounded-xl',
              tone === 'warn' ? 'bg-destructive/10 text-destructive' : 'bg-accent text-accent-foreground',
            )}
          >
            <Icon className="size-4.5" />
          </span>
        </div>
        <p className="text-2xl font-bold tracking-tight wrap-break-word">{value}</p>
        {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
      </CardContent>
    </Card>
  )
}
