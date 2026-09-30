import { useTranslation } from 'react-i18next'
import { CopyIcon, TicketPercentIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { usePublicVouchers } from '@/hooks/voucher'
import type { PublicVoucher, PublicVoucherScope } from '@/services/voucher/types'
import { describeBenefit, describeRules } from '@/utils/voucher/describe'

/**
 * The vouchers a shopper could use here (specs/114): what each gives, until when, and its code to copy - or, at
 * checkout, to use. Nothing is drawn while there are none, or while the list has not answered: a promotion is an
 * extra, never something a page waits for.
 */
export function PublicVouchers({
  scope,
  title,
  onUse,
  usedCodes = [],
}: {
  /** Whose vouchers - null while the page does not know yet. */
  scope: PublicVoucherScope | null
  title?: string
  /** At checkout: apply it, as typing its code would. */
  onUse?: (code: string) => void
  usedCodes?: string[]
}) {
  const { t, i18n } = useTranslation('vouchers')
  const vouchers = usePublicVouchers(scope)
  const list = (vouchers.data ?? []).filter((v) => !usedCodes.includes(v.code))
  if (list.length === 0) return null

  const copy = (code: string) =>
    navigator.clipboard?.writeText(code).then(() => toast.success(t('public.copied', { code })), () => undefined)

  return (
    <section className="grid gap-2" aria-label={title ?? t('public.title')}>
      <h2 className="flex items-center gap-1.5 text-sm font-semibold">
        <TicketPercentIcon className="text-primary size-4" /> {title ?? t('public.title')}
      </h2>
      <ul className="grid gap-2">
        {list.map((voucher) => (
          <li key={voucher.code} className="bg-card ring-border/60 flex flex-wrap items-center justify-between gap-2 rounded-2xl p-3 ring-1">
            <div className="grid gap-0.5 text-sm">
              <span className="flex flex-wrap items-center gap-2">
                <span className="font-mono font-semibold">{voucher.code}</span>
                <span className="text-muted-foreground">{voucher.name}</span>
              </span>
              <span>{describeBenefit(t, asSummary(voucher)).join(' · ')}</span>
              <span className="text-muted-foreground text-xs">
                {describeRules(t, asSummary(voucher)).join(' · ')}
                {voucher.endsAt && ` · ${t('until', { at: new Date(voucher.endsAt).toLocaleDateString(i18n.language) })}`}
              </span>
            </div>
            {onUse ? (
              <Button type="button" size="sm" variant="outline" className="rounded-full" onClick={() => onUse(voucher.code)}>
                {t('public.use')}
              </Button>
            ) : (
              <Button type="button" size="sm" variant="ghost" className="rounded-full" onClick={() => void copy(voucher.code)} aria-label={t('public.copy', { code: voucher.code })}>
                <CopyIcon /> {t('public.copyShort')}
              </Button>
            )}
          </li>
        ))}
      </ul>
    </section>
  )
}

/** The owner's wording helpers read a summary; a public voucher carries one currency and says only whether it targets. */
function asSummary(voucher: PublicVoucher) {
  return {
    benefit: voucher.benefit,
    percent: voucher.percent,
    amounts: [{ currency: voucher.currency, fixedValue: voucher.fixedValue, maxDiscount: voucher.maxDiscount, minSubtotal: voucher.minSubtotal }],
    conditions: voucher.conditions,
    targets: voucher.targeted ? [{ type: 'Product', id: '' }] : [],
  }
}
