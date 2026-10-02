import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { BanIcon, TicketPercentIcon, SearchIcon } from 'lucide-react'
import { toast } from 'sonner'
import { PageTitle } from '@ecommerce/core/components/seller/page-title'
import { Pager } from '@ecommerce/core/components/shared/pager'
import { TabStrip } from '@ecommerce/core/components/shared/tab-strip'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@ecommerce/ui/input-group'
import { VoucherEdit } from '@ecommerce/core/components/voucher/voucher-edit'
import { VoucherForm } from '@ecommerce/core/components/voucher/voucher-form'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@ecommerce/ui/alert-dialog'
import { Badge } from '@ecommerce/ui/badge'
import { Button } from '@ecommerce/ui/button'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { useDisableVoucher, useMyVouchers, useMyVoucherCounts } from '@ecommerce/core/hooks/voucher'
import { VOUCHER_STATES, type VoucherState } from '@ecommerce/core/services/voucher/types'
import type { VoucherSummary } from '@ecommerce/core/services/voucher/types'
import { cn } from 'cn'
import { describeBenefit, describeRules, describeUses } from '@ecommerce/core/utils/voucher/describe'

/**
 * The caller's vouchers (specs/070): a seller's shop's in their console, the platform's in the administrator's -
 * one page, because the server answers "mine" for both and the list is the same list (research D2).
 *
 * <p>
 * Searched by code or name and filtered by state, each tab counting what it holds for the search (specs/133, #249) -
 * the administrator's list was 58 tall cards with no way to find one. Both live in the address.
 * </p>
 */
export function VoucherPage({ platform }: { platform: boolean }) {
  const { t } = useTranslation('vouchers')
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const search = params.get('q') ?? ''
  const state = (VOUCHER_STATES as readonly string[]).includes(params.get('state') ?? '') ? (params.get('state') as VoucherState) : ''
  const [draft, setDraft] = useState(search)
  const vouchers = useMyVouchers(page, PAGE_SIZE, { search, state })
  const counts = useMyVoucherCounts(search)
  const go = (next: { page?: number; q?: string; state?: string }) => {
    const merged = new URLSearchParams()
    const q = next.q ?? search
    const s = next.state ?? state
    if (q) merged.set('q', q)
    if (s) merged.set('state', s)
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }
  const disable = useDisableVoucher()

  return (
    <section className="grid gap-6">
      <PageTitle title={t('title')} subtitle={t(platform ? 'subtitlePlatform' : 'subtitleShop')} />
      <VoucherForm platform={platform} />
      <ServerError error={disable.error} fallback={t('loadFailed')} />

      <div className="flex flex-wrap items-center gap-3">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            go({ q: draft.trim(), page: 1 })
          }}
          className="min-w-56 flex-1"
        >
          <InputGroup className="h-10 rounded-full">
            <InputGroupAddon>
              <SearchIcon />
            </InputGroupAddon>
            <InputGroupInput
              aria-label={t('filter.search')}
              placeholder={t('filter.search')}
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
            />
          </InputGroup>
        </form>
        <TabStrip
          tabs={(['', ...VOUCHER_STATES] as const).map((value) => {
            const label = t(`filter.state.${value || 'All'}`)
            const count = counts[value || 'All']
            return { value, label: count === undefined ? label : `${label} ${count}` }
          })}
          current={state}
          onChange={(next) => go({ state: next, page: 1 })}
        />
      </div>

      {vouchers.isError ? (
        <ErrorMessage>{t('loadFailed')}</ErrorMessage>
      ) : vouchers.isPending || !vouchers.data ? (
        <LoadingRows />
      ) : vouchers.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {vouchers.data.items.map((voucher) => (
              <VoucherRow key={voucher.id} voucher={voucher} onDisable={() => disable.mutate(voucher.id, { onSuccess: () => toast.success(t('disabled')) })} busy={disable.isPending} />
            ))}
          </ul>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={vouchers.data.totalCount} onChange={(next) => go({ page: next })} />
        </>
      )}
    </section>
  )
}

function VoucherRow({ voucher, onDisable, busy }: { voucher: VoucherSummary; onDisable: () => void; busy: boolean }) {
  const { t, i18n } = useTranslation('vouchers')
  const active = voucher.status === 'Active'
  const day = (at: string) => new Date(at).toLocaleDateString(i18n.language)

  return (
    <li className={cn('bg-card ring-border/60 grid gap-2 rounded-3xl p-4 ring-1', !active && 'opacity-70')}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="flex items-center gap-2">
          <TicketPercentIcon className="text-primary size-4.5" />
          <span className="font-mono font-semibold">{voucher.code}</span>
          <span className="text-muted-foreground text-sm">{voucher.name}</span>
        </span>
        <span className="flex gap-1.5">
          <Badge variant="outline">{t(voucher.isPublic ? 'public.shown' : 'public.codeOnly')}</Badge>
          <Badge variant={active ? 'default' : 'outline'}>{t(`status.${voucher.status}`, { defaultValue: voucher.status })}</Badge>
        </span>
      </div>
      <ul className="grid gap-0.5 text-sm">
        {describeBenefit(t, voucher).map((line) => (
          <li key={line}>{line}</li>
        ))}
      </ul>
      <p className="text-muted-foreground text-xs">{describeRules(t, voucher).join(' · ')}</p>
      <p className="text-muted-foreground text-xs">
        {describeUses(t, voucher)} · {t('from', { at: day(voucher.startsAt) })}
        {voucher.endsAt ? ` ${t('until', { at: day(voucher.endsAt) })}` : ''}
      </p>
      {active && (
        <div className="flex flex-wrap gap-2">
          <VoucherEdit voucher={voucher} />
          <AlertDialog>
            <AlertDialogTrigger render={<Button variant="outline" size="sm" className="text-destructive rounded-full" disabled={busy} />}>
              <BanIcon /> {t('disable')}
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>{t('disableTitle', { code: voucher.code })}</AlertDialogTitle>
                <AlertDialogDescription>{t('disableBody')}</AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>{t('action.cancel', { ns: 'common' })}</AlertDialogCancel>
                <AlertDialogAction variant="destructive" onClick={onDisable}>
                  {t('disable')}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      )}
    </li>
  )
}
