import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { SELLER_RETURN_STATES, type SellerReturnState } from '@ecommerce/core/constants/order'
import { useSaleReturns } from '@ecommerce/core/hooks/order'
import { cn } from 'cn'

/**
 * The returns of the seller's own parcels (specs/108), one state at a time, oldest waiting first. It opens on the ones
 * asking for their decision; each row leads to the sale, where the steps are. No amounts, as on staff's page: a return
 * carries no currency of its own, and the sale shows the refund in the order's.
 */
export function ShopReturnsPage() {
  const { t, i18n } = useTranslation('seller')
  const [params, setParams] = useSearchParams()
  const asked = params.get('status') as SellerReturnState | null
  const status: SellerReturnState = asked && SELLER_RETURN_STATES.includes(asked) ? asked : 'Requested'
  const page = Number(params.get('page') ?? '1') || 1
  const returns = useSaleReturns(status, page, PAGE_SIZE)

  return (
    <section className="grid gap-6">
      <PageTitle title={t('returns.title')} subtitle={t('returns.subtitle')} />

      <div role="tablist" className="bg-card ring-border/60 flex flex-wrap gap-1 justify-self-start rounded-3xl p-1 ring-1">
        {SELLER_RETURN_STATES.map((state) => (
          <button
            key={state}
            type="button"
            role="tab"
            aria-selected={state === status}
            onClick={() => setParams({ status: state })}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              state === status ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(`returns.tab.${state}`)}
          </button>
        ))}
      </div>

      {returns.isError ? (
        <ErrorMessage>{t('returns.loadFailed')}</ErrorMessage>
      ) : returns.isPending || !returns.data ? (
        <LoadingRows />
      ) : returns.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('returns.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {returns.data.items.map((ret) => (
              <li key={ret.id}>
                <Link
                  to={`/shop/sales/${ret.orderId}`}
                  className="bg-card ring-border/60 hover:ring-primary/60 grid gap-1 rounded-3xl p-4 ring-1 transition-all"
                >
                  <span className="font-medium">
                    {t('returns.requestedAt', { at: new Date(ret.requestedAt).toLocaleString(i18n.language), order: ret.orderId.slice(0, 8) })}
                  </span>
                  <span className="line-clamp-2 text-sm">{ret.reason}</span>
                  {ret.trackingReference && (
                    <span className="text-muted-foreground text-xs">{t('returns.sentWith', { reference: ret.trackingReference })}</span>
                  )}
                </Link>
              </li>
            ))}
          </ul>
          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={returns.data.totalCount}
            onChange={(next) => setParams({ status, page: String(next) })}
          />
        </>
      )}
    </section>
  )
}
