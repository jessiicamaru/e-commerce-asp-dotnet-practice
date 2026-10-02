import { useState, type FormEvent } from 'react'
import { useQuery, useQueries } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useSearchParams, useLocation } from 'react-router-dom'
import { OrderRow } from '@ecommerce/core/components/order/order-row'
import { SearchIcon } from 'lucide-react'
import { PageTitle } from '@ecommerce/core/components/seller/page-title'
import { Pager } from '@ecommerce/core/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { Button } from '@ecommerce/ui/button'
import { Input } from '@ecommerce/ui/input'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Admin } from '@ecommerce/core/services/admin'
import { useStaffOrderSearch } from '@ecommerce/core/hooks/admin'
import { Accounts } from '@ecommerce/core/services/accounts'
import { STAFF_ORDER_STATES, type StaffOrderState } from '@ecommerce/core/services/admin/types'
import { Insights } from '@ecommerce/core/services/insights'
import { cn } from 'cn'
import { describeOrderStatus } from '@ecommerce/core/utils/order'

/**
 * Find any order (specs/096): by the start of its id, or by the customer's email, in any status. Order knows people only
 * by id, so an email is first turned into a person through Identity's staff search - the page composes the two, as the
 * overview does (specs/047). What was asked is in the address (`?q=&status=&page=`).
 */
export function AdminOrderSearchPage() {
  const { t } = useTranslation('admin')
  const location = useLocation()
  const [params, setParams] = useSearchParams()
  const q = (params.get('q') ?? '').trim()
  const asked = params.get('status')
  const status = STAFF_ORDER_STATES.includes(asked as StaffOrderState) ? (asked as StaffOrderState) : undefined
  const page = Number(params.get('page') ?? '1') || 1
  const [draft, setDraft] = useState(q)

  const byEmail = q.includes('@')
  const people = useQuery({
    queryKey: queryKeys.accounts(q, 1),
    queryFn: () => Accounts.search(q, 1, 5),
    enabled: byEmail,
  })
  const customer = byEmail ? people.data?.items.find((a) => a.email.toLowerCase() === q.toLowerCase()) : undefined
  const nobody = byEmail && people.isSuccess && !customer

  const searchable = !byEmail || customer !== undefined
  const orders = useStaffOrderSearch(
    { status, search: byEmail || !q ? undefined : q, customerId: customer?.id, page, pageSize: PAGE_SIZE },
    searchable,
  )
  // How many orders each state holds for this search (specs/133): a page of one each - the query is the key, so the
  // size keeps them apart from the list's own page (specs/130).
  const counts = useQueries({
    queries: [undefined, ...STAFF_ORDER_STATES].map((state) => ({
      queryKey: queryKeys.staffOrders({ status: state, search: byEmail || !q ? undefined : q, customerId: customer?.id, page: 1, pageSize: 1 }),
      queryFn: () => Admin.findOrders({ status: state, search: byEmail || !q ? undefined : q, customerId: customer?.id, page: 1, pageSize: 1 }),
      enabled: searchable,
    })),
    combine: (results) => results.map((result) => result.data?.totalCount),
  })
  const { t: tOrders } = useTranslation('orders')

  const ids = [...new Set((orders.data?.items ?? []).map((o) => o.userId))]
  const who = useQuery({ queryKey: queryKeys.people(ids), queryFn: () => Insights.people(ids), enabled: ids.length > 0 })
  const personOf = (id: string) => who.data?.find((p) => p.id === id)

  const go = (next: { q?: string; status?: StaffOrderState | null; page?: number }) => {
    const merged = new URLSearchParams()
    const nextQ = next.q ?? q
    const nextStatus = next.status === null ? undefined : (next.status ?? status)
    if (nextQ) merged.set('q', nextQ)
    if (nextStatus) merged.set('status', nextStatus)
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    go({ q: draft.trim(), page: 1 })
  }

  const failed = people.isError || orders.isError

  return (
    <section className="grid gap-6">
      <PageTitle title={t('findOrder.title')} subtitle={t('findOrder.subtitle')} />

      <form onSubmit={submit} className="flex flex-wrap gap-2" role="search">
        <Input
          aria-label={t('findOrder.search')}
          placeholder={t('findOrder.placeholder')}
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          className="max-w-md rounded-full"
        />
        <Button type="submit" className="rounded-full">
          <SearchIcon /> {t('findOrder.find')}
        </Button>
      </form>

      <div role="tablist" className="bg-card ring-border/60 flex flex-wrap gap-1 justify-self-start rounded-full p-1 ring-1">
        {[undefined, ...STAFF_ORDER_STATES].map((state, index) => (
          <button
            key={state ?? 'all'}
            type="button"
            role="tab"
            aria-selected={state === status}
            onClick={() => go({ status: state ?? null, page: 1 })}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              state === status ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {state ? t(`findOrder.state.${state}`) : t('findOrder.all')}
            {counts[index] !== undefined && <span className="ml-1.5 tabular-nums opacity-70">{counts[index]}</span>}
          </button>
        ))}
      </div>

      {failed ? (
        <ErrorMessage>{t('findOrder.loadFailed')}</ErrorMessage>
      ) : nobody ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('findOrder.nobody', { email: q })}</p>
      ) : orders.isPending || !orders.data ? (
        <LoadingRows />
      ) : orders.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('findOrder.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {orders.data.items.map((order) => {
              const person = personOf(order.userId)
              return (
                <li key={order.orderId}>
                  {/* Reference, products and status (specs/132), with whose it is; where it was opened from rides
                      along (specs/129). */}
                  <OrderRow
                    to={`/orders/${order.orderId}`}
                    state={{ from: `${location.pathname}${location.search}` }}
                    orderId={order.orderId}
                    lines={order.lines}
                    lineCount={order.itemCount}
                    createdAt={order.createdAt}
                    status={order.status}
                    shipped={order.shipmentsShipped}
                    parcels={order.shipmentCount}
                    amount={order.totalAmount}
                    currency={order.currency}
                    detail={
                      <>
                        <span className="text-muted-foreground text-xs">
                          {person ? `${person.firstName} ${person.lastName} · ${person.email}` : t('findOrder.someone')}
                        </span>
                        {/* In the customer's words (specs/133) - not the server's text with a product id in it. */}
                        {order.failureReason && (
                          <span className="text-destructive text-xs">{describeOrderStatus(tOrders, order.status, order.failureReason)}</span>
                        )}
                      </>
                    }
                  />
                </li>
              )
            })}
          </ul>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={orders.data.totalCount} onChange={(next) => go({ page: next })} />
        </>
      )}
    </section>
  )
}
