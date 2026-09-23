import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { Price } from '@/components/shared/price'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useAuth } from '@/context/auth/useAuth'
import { useMyBalance, useMyPayouts } from '@/hooks/order'
import { PAGE_SIZE } from '@/constants/shared'

/**
 * What the shop owes this seller and what it has paid them (specs/037).
 *
 * <p>
 * <b>One card per currency</b>: dong and dollars do not add up and the shop converts nothing
 * (specs/022). Each card has the three places money can be - on its way (paid, not sent), due (sent,
 * not paid out) and paid out - because "how much will I get" and "how much am I waiting for" are
 * different questions and one number answers neither.
 * </p>
 * <p>
 * Payment is a stub: a payout here is the shop's record that it settled, not a transfer this system
 * made. The page says so.
 * </p>
 */
export function ShopPayoutsPage() {
  const { t, i18n } = useTranslation('seller')
  const { isSeller } = useAuth()
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const balance = useMyBalance(isSeller)
  const payouts = useMyPayouts(page, PAGE_SIZE, isSeller)

  if (balance.isError || payouts.isError) {
    return <ErrorMessage>{t('payouts.loadFailed')}</ErrorMessage>
  }

  if (balance.isPending || payouts.isPending || !balance.data || !payouts.data) {
    return <LoadingRows rows={3} />
  }

  return (
    <section className="grid gap-6">
      <PageTitle title={t('payouts.title')} subtitle={t('payouts.subtitle')} />

      {balance.data.length === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('payouts.nothingYet')}</p>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {balance.data.map((b) => (
            <Card key={b.currency} className="rounded-3xl" aria-label={t('payouts.balanceIn', { currency: b.currency })}>
              <CardHeader>
                <CardTitle>{t('payouts.balanceIn', { currency: b.currency })}</CardTitle>
              </CardHeader>
              <CardContent>
                <dl className="grid grid-cols-3 gap-3">
                  <Figure label={t('earnings.state.onTheWay')} value={b.onTheWay} currency={b.currency} />
                  <Figure label={t('earnings.state.due')} value={b.due} currency={b.currency} strong />
                  <Figure label={t('earnings.state.paidOut')} value={b.paidOut} currency={b.currency} />
                </dl>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Card className="rounded-3xl">
        <CardHeader>
          <CardTitle>{t('payouts.history')}</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4">
          {payouts.data.totalCount === 0 ? (
            <p className="text-muted-foreground text-sm">{t('payouts.none')}</p>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('payouts.columns.date')}</TableHead>
                    <TableHead>{t('payouts.columns.sales')}</TableHead>
                    <TableHead className="text-right">{t('payouts.columns.amount')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {payouts.data.items.map((p) => (
                    <TableRow key={p.id}>
                      <TableCell>{new Date(p.createdAt).toLocaleString(i18n.language)}</TableCell>
                      <TableCell>{t('payouts.parts', { count: p.partCount })}</TableCell>
                      <TableCell className="text-right font-semibold tabular-nums">
                        <Price value={p.amount} currency={p.currency} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <Pager
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={payouts.data.totalCount}
                onChange={(next) => setParams({ page: String(next) })}
              />
            </>
          )}
        </CardContent>
      </Card>
    </section>
  )
}

function Figure({ label, value, currency, strong }: { label: string; value: number; currency: string; strong?: boolean }) {
  return (
    <div className="grid gap-1">
      <dt className="text-muted-foreground text-xs">{label}</dt>
      <dd className={strong ? 'text-lg font-bold tabular-nums' : 'font-semibold tabular-nums'}>
        <Price value={value} currency={currency} />
      </dd>
    </div>
  )
}
