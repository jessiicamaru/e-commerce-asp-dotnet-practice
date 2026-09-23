import { useTranslation } from 'react-i18next'
import { Price } from '@/components/shared/price'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import type { Sale } from '@/services/order/types'
import { earningState } from './state'

/**
 * What one sale earns the seller (specs/037): their goods, less the marketplace's commission, plus their
 * share of the delivery charge - and where that money is now.
 *
 * <p>
 * Every number is the one recorded at checkout, in the order's own currency. Nothing is computed here:
 * the server sends the payout too, so a rounding rule cannot differ between the page and the ledger.
 * An order from before the terms were recorded says so, rather than showing zeros that read as "you
 * earned nothing".
 * </p>
 */
export function SaleEarnings({ sale }: { sale: Sale }) {
  const { t } = useTranslation('seller')
  const state = earningState(sale)

  if (state === 'unrecorded') {
    return <p className="text-muted-foreground text-sm">{t('earnings.unrecorded')}</p>
  }

  const row = (label: string, value: number | null, sign = '') => (
    <div className="flex items-baseline justify-between gap-4 text-sm">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="tabular-nums">
        {sign}
        <Price value={value} currency={sale.currency} />
      </dd>
    </div>
  )

  return (
    <div className="grid gap-3">
      <dl className="grid gap-2">
        {row(t('earnings.goods'), sale.goodsTotal)}
        {row(t('earnings.commission'), sale.commission, '− ')}
        {row(t('earnings.shippingShare'), sale.shippingShare, '+ ')}
        <Separator />
        <div className="flex items-baseline justify-between gap-4">
          <dt className="font-semibold">{t('earnings.payout')}</dt>
          <dd className="text-lg font-bold tabular-nums">
            <Price value={sale.payout} currency={sale.currency} />
          </dd>
        </div>
      </dl>
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant={state === 'paidOut' ? 'default' : state === 'due' ? 'secondary' : 'outline'}>
          {t(`earnings.state.${state}`)}
        </Badge>
        <p className="text-muted-foreground text-xs">{t(`earnings.hint.${state}`)}</p>
      </div>
    </div>
  )
}
