import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { SearchIcon } from 'lucide-react'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { Price } from '@/components/shared/price'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { PAGE_SIZE } from '@/constants/shared'
import { queryKeys } from '@/constants/query-keys'
import { useStaffOrderSearch } from '@/hooks/admin'
import { Accounts } from '@/services/accounts'
import { STAFF_ORDER_STATES, type StaffOrderState } from '@/services/admin/types'
import { Insights } from '@/services/insights'
import { cn } from '@/utils/shared'

/**
 * Find any order (specs/096): by the start of its id, or by the customer's email, in any status. Order knows people only
 * by id, so an email is first turned into a person through Identity's staff search - the page composes the two, as the
 * overview does (specs/047). What was asked is in the address (`?q=&status=&page=`).
 */
export function AdminOrderSearchPage() {
  const { t, i18n } = useTranslation('admin')
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

  const orders = useStaffOrderSearch(
    { status, search: byEmail || !q ? undefined : q, customerId: customer?.id, page, pageSize: PAGE_SIZE },
    !byEmail || customer !== undefined,
  )

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
        {[undefined, ...STAFF_ORDER_STATES].map((state) => (
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
                  <Link
                    to={`/admin/orders/${order.orderId}`}
                    className="bg-card ring-border/60 hover:ring-primary/60 grid gap-1 rounded-3xl p-4 ring-1 transition-all"
                  >
                    <span className="flex flex-wrap items-center gap-2 font-medium">
                      <span className="font-mono text-sm">{order.orderId.slice(0, 8)}</span>
                      <span className="text-muted-foreground text-xs">{t(`findOrder.state.${order.status}`, { defaultValue: order.status })}</span>
                    </span>
                    <span className="text-sm">
                      {person ? `${person.firstName} ${person.lastName} · ${person.email}` : t('findOrder.someone')}
                    </span>
                    <span className="text-sm">
                      {new Date(order.createdAt).toLocaleString(i18n.language)} · {t('queue.items', { count: order.itemCount })} ·{' '}
                      <Price value={order.totalAmount} currency={order.currency} className="font-semibold" />
                    </span>
                    {order.failureReason && <span className="text-destructive text-xs">{order.failureReason}</span>}
                  </Link>
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
