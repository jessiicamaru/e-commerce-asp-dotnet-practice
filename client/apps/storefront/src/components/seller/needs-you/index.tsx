import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ArrowRightIcon, CheckCircle2Icon, MessageCircleQuestionIcon, ReceiptTextIcon, Undo2Icon, WalletIcon } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { SellerWaiting } from '@ecommerce/core/hooks/order'

/**
 * What waits for the seller, each with the way to it (specs/131, #247): sales to prepare, questions to answer, returns
 * to decide, and a missing payout account. A sale waiting to be prepared was a line of small grey text on the home,
 * and a missing payout account was said only on Payouts.
 */
export function NeedsYou({ waiting }: { waiting: SellerWaiting }) {
  const { t } = useTranslation('seller')
  const items: { to: string; icon: LucideIcon; text: string }[] = [
    ...(waiting.toPrepare ? [{ to: '/shop/sales', icon: ReceiptTextIcon, text: t('overview.needs.toPrepare', { count: waiting.toPrepare }) }] : []),
    ...(waiting.questions ? [{ to: '/shop/questions', icon: MessageCircleQuestionIcon, text: t('overview.needs.questions', { count: waiting.questions }) }] : []),
    ...(waiting.returns ? [{ to: '/shop/returns', icon: Undo2Icon, text: t('overview.needs.returns', { count: waiting.returns }) }] : []),
    ...(waiting.payoutAccount === false ? [{ to: '/shop/payouts', icon: WalletIcon, text: t('overview.needs.payoutAccount') }] : []),
  ]

  return (
    <section aria-label={t('overview.needs.title')} className="bg-card ring-border/60 grid gap-2 rounded-3xl p-4 ring-1">
      <h2 className="px-1 font-semibold">{t('overview.needs.title')}</h2>
      {items.length === 0 ? (
        <p className="text-muted-foreground flex items-center gap-2 px-1 text-sm">
          <CheckCircle2Icon className="size-4 text-emerald-600" /> {t('overview.needs.none')}
        </p>
      ) : (
        <ul className="grid gap-1">
          {items.map(({ to, icon: Icon, text }) => (
            <li key={to}>
              <Link to={to} className="hover:bg-secondary/70 flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium">
                <Icon className="text-primary-foreground bg-primary size-7 shrink-0 rounded-lg p-1.5" />
                <span className="flex-1">{text}</span>
                <ArrowRightIcon className="text-muted-foreground size-4" />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
