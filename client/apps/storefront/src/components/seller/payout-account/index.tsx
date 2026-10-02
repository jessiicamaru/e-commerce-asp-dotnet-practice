import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LandmarkIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { LoadingRows } from '@ecommerce/core/components/query-state'
import { Button } from '@ecommerce/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@ecommerce/ui/card'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { useMyPayoutAccount, useSetPayoutAccount } from '@ecommerce/core/hooks/seller'

/**
 * Where a seller's payouts go (#213, specs/106). The number is shown masked - the server never sends it back whole -
 * and every change emails the seller, which the card says, so a change they did not make is noticed.
 */
export function PayoutAccountCard() {
  const { t, i18n } = useTranslation('seller')
  const account = useMyPayoutAccount()
  const save = useSetPayoutAccount()
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState({ bankName: '', accountHolder: '', accountNumber: '' })

  if (account.isPending) return <LoadingRows rows={1} />
  const current = account.data ?? null
  const showForm = editing || current === null

  return (
    <Card className="rounded-3xl">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <LandmarkIcon className="size-4.5" /> {t('payoutAccount.title')}
        </CardTitle>
        <CardDescription>{t('payoutAccount.hint')}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        {current && !editing && (
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="grid gap-0.5 text-sm">
              <p className="font-semibold">{current.bankName} · {current.accountHolder}</p>
              <p className="font-mono">{current.accountNumberMasked}</p>
              <p className="text-muted-foreground text-xs">
                {t('payoutAccount.updated', { at: new Date(current.updatedAt).toLocaleString(i18n.language) })}
              </p>
            </div>
            <Button
              variant="outline"
              className="rounded-full"
              onClick={() => {
                setForm({ bankName: current.bankName, accountHolder: current.accountHolder, accountNumber: '' })
                save.reset()
                setEditing(true)
              }}
            >
              {t('payoutAccount.change')}
            </Button>
          </div>
        )}
        {current === null && !editing && <p className="text-muted-foreground text-sm">{t('payoutAccount.none')}</p>}

        {showForm && (
          <form
            className="grid gap-3 sm:grid-cols-3"
            onSubmit={(event) => {
              event.preventDefault()
              save.mutateAsync({
                bankName: form.bankName.trim(),
                accountHolder: form.accountHolder.trim(),
                accountNumber: form.accountNumber.trim(),
              }).then(() => {
                toast.success(t('payoutAccount.saved'))
                setEditing(false)
              }, () => {})
            }}
          >
            <div className="grid gap-1.5">
              <Label htmlFor="payout-bank">{t('payoutAccount.bank')}</Label>
              <Input id="payout-bank" value={form.bankName} maxLength={100} onChange={(e) => setForm((f) => ({ ...f, bankName: e.target.value }))} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="payout-holder">{t('payoutAccount.holder')}</Label>
              <Input id="payout-holder" value={form.accountHolder} maxLength={100} onChange={(e) => setForm((f) => ({ ...f, accountHolder: e.target.value }))} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="payout-number">{t('payoutAccount.number')}</Label>
              <Input id="payout-number" inputMode="numeric" autoComplete="off" value={form.accountNumber} maxLength={45}
                onChange={(e) => setForm((f) => ({ ...f, accountNumber: e.target.value }))} />
            </div>
            <div className="grid gap-2 sm:col-span-3">
              <ServerError error={save.error} fallback={t('payouts.loadFailed')} />
              <p className="text-muted-foreground text-xs">{t('payoutAccount.emailed')}</p>
              <div className="flex justify-end gap-2">
                {editing && current && (
                  <Button type="button" variant="ghost" className="rounded-full" onClick={() => setEditing(false)}>
                    {t('action.cancel', { ns: 'common' })}
                  </Button>
                )}
                <Button
                  type="submit"
                  className="rounded-full"
                  disabled={save.isPending || !form.bankName.trim() || !form.accountHolder.trim() || !form.accountNumber.trim()}
                >
                  {t('payoutAccount.save')}
                </Button>
              </div>
            </div>
          </form>
        )}
      </CardContent>
    </Card>
  )
}
