import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { WalletIcon } from 'lucide-react'
import { PageTitle } from '@/components/seller/page-title'
import { Price } from '@/components/shared/price'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { usePaySeller, usePayoutsDue } from '@/hooks/admin'
import type { PayoutDue } from '@/services/admin/types'
import { money } from '@/utils/shared'

/**
 * What the shop owes each seller now, and the button that records paying it (specs/037, 038).
 *
 * <p>
 * The payout is confirmed first, naming the seller and the amount this list showed - it is a ledger
 * entry that cannot be undone. The server pays what is due AT THAT MOMENT, which is more if another
 * parcel shipped since the list loaded, so the confirmation afterwards says what was actually recorded
 * (research D3). Somebody else having paid first comes back as the server's 409, in its own words.
 * </p>
 */
export function AdminPayoutsPage() {
  const { t } = useTranslation('admin')
  const due = usePayoutsDue()
  const pay = usePaySeller()
  // One dialog for the page, open while a row is being confirmed. Controlled, because the dialog's
  // action button does not close it on its own - left open, it hid the refusal behind it.
  const [confirming, setConfirming] = useState<PayoutDue | null>(null)

  if (due.isError) {
    return <ErrorMessage>{t('payouts.loadFailed')}</ErrorMessage>
  }

  if (due.isPending) {
    return <LoadingRows />
  }

  const nameOf = (row: PayoutDue) => row.sellerName ?? t('payouts.unnamed', { id: row.sellerId.slice(0, 8) })

  const record = (row: PayoutDue) =>
    pay.mutate(
      { sellerId: row.sellerId, currency: row.currency },
      {
        onSuccess: (payout) =>
          toast.success(t('payouts.paid', { amount: money(payout.amount, payout.currency), seller: nameOf(row) })),
      },
    )

  return (
    <section className="grid gap-6">
      <PageTitle title={t('payouts.title')} subtitle={t('payouts.subtitle')} />
      <ServerError error={pay.error} fallback={t('payouts.loadFailed')} />

      {due.data.length === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('payouts.none')}</p>
      ) : (
        <div className="bg-card ring-border/60 rounded-3xl p-2 ring-1">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('payouts.columns.seller')}</TableHead>
                <TableHead>{t('payouts.columns.parts')}</TableHead>
                <TableHead className="text-right">{t('payouts.columns.due')}</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {due.data.map((row) => (
                <TableRow key={`${row.sellerId}-${row.currency}`}>
                  <TableCell className="font-medium whitespace-normal">{nameOf(row)}</TableCell>
                  <TableCell>{row.parts}</TableCell>
                  <TableCell className="text-right font-semibold tabular-nums">
                    <Price value={row.due} currency={row.currency} />
                  </TableCell>
                  <TableCell className="text-right">
                    <Button size="sm" className="rounded-full px-3" disabled={pay.isPending} onClick={() => setConfirming(row)}>
                      <WalletIcon /> {t('payouts.pay')}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <AlertDialog open={confirming !== null} onOpenChange={(open) => !open && setConfirming(null)}>
        {confirming && (
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t('payouts.confirmTitle', { seller: nameOf(confirming) })}</AlertDialogTitle>
              <AlertDialogDescription>
                {t('payouts.confirmBody', { amount: money(confirming.due, confirming.currency), count: confirming.parts })}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>{t('action.cancel', { ns: 'common' })}</AlertDialogCancel>
              <AlertDialogAction
                onClick={() => {
                  record(confirming)
                  setConfirming(null)
                }}
              >
                {t('payouts.confirm')}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        )}
      </AlertDialog>
    </section>
  )
}
