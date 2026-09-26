import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BanIcon, TicketPercentIcon } from 'lucide-react'
import { toast } from 'sonner'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { VoucherForm } from '@/components/voucher/voucher-form'
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
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PAGE_SIZE } from '@/constants/shared'
import { useDisableVoucher, useMyVouchers } from '@/hooks/voucher'
import type { VoucherSummary } from '@/services/voucher/types'
import { cn } from '@/utils/shared'
import { describeBenefit, describeRules, describeUses } from '@/utils/voucher/describe'

/**
 * The caller's vouchers (specs/070): a seller's shop's in their console, the platform's in the administrator's -
 * one page, because the server answers "mine" for both and the list is the same list (research D2).
 */
export function VoucherPage({ platform }: { platform: boolean }) {
  const { t } = useTranslation('vouchers')
  const [page, setPage] = useState(1)
  const vouchers = useMyVouchers(page, PAGE_SIZE)
  const disable = useDisableVoucher()

  return (
    <section className="grid gap-6">
      <PageTitle title={t('title')} subtitle={t(platform ? 'subtitlePlatform' : 'subtitleShop')} />
      <VoucherForm platform={platform} />
      <ServerError error={disable.error} fallback={t('loadFailed')} />

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
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={vouchers.data.totalCount} onChange={setPage} />
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
        <Badge variant={active ? 'default' : 'outline'}>{t(`status.${voucher.status}`, { defaultValue: voucher.status })}</Badge>
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
        <AlertDialog>
          <AlertDialogTrigger render={<Button variant="outline" size="sm" className="text-destructive justify-self-start rounded-full" disabled={busy} />}>
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
      )}
    </li>
  )
}
