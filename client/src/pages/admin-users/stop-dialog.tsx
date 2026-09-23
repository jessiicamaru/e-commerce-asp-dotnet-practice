import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { Account } from '@/services/accounts/types'
import { cn } from '@/utils/shared'

export type Stop = 'lock' | 'ban'

/** The lengths offered; an administrator also gets the longer ones (specs/043). */
const DAYS = [1, 3, 7, 14, 30, 90, 365]

/**
 * Asks how long and why before an account is locked, or why before it is banned (specs/043). The reason
 * is required - the person reads it when they next try to sign in.
 */
export function StopDialog({
  stopping,
  maxDays,
  onClose,
  onConfirm,
}: {
  stopping: { kind: Stop; account: Account } | null
  /** A moderator's ceiling; undefined for an administrator. The server holds the same line on its own. */
  maxDays?: number
  onClose: () => void
  onConfirm: (kind: Stop, account: Account, days: number, reason: string) => void
}) {
  return (
    <Dialog open={stopping !== null} onOpenChange={(open) => !open && onClose()}>
      {stopping && (
        // Keyed on the account, so the reason typed for one person is not left in the box for the next.
        <StopForm key={`${stopping.kind}-${stopping.account.id}`} {...stopping} maxDays={maxDays} onConfirm={onConfirm} />
      )}
    </Dialog>
  )
}

function StopForm({
  kind,
  account,
  maxDays,
  onConfirm,
}: {
  kind: Stop
  account: Account
  maxDays?: number
  onConfirm: (kind: Stop, account: Account, days: number, reason: string) => void
}) {
  const { t } = useTranslation('admin')
  const [days, setDays] = useState(3)
  const [reason, setReason] = useState('')
  const offered = DAYS.filter((d) => maxDays === undefined || d <= maxDays)

  return (
    <DialogContent>
      <form
        className="grid gap-4"
        onSubmit={(event) => {
          event.preventDefault()
          if (reason.trim()) onConfirm(kind, account, days, reason.trim())
        }}
      >
        <DialogHeader>
          <DialogTitle>{t(kind === 'lock' ? 'users.lockTitle' : 'users.banTitle', { email: account.email })}</DialogTitle>
          <DialogDescription>{t(kind === 'lock' ? 'users.lockBody' : 'users.banBody')}</DialogDescription>
        </DialogHeader>

        {kind === 'lock' && (
          <div className="grid gap-2">
            <p className="text-sm font-medium">{t('users.days')}</p>
            <div className="flex flex-wrap gap-1.5">
              {offered.map((d) => (
                <button
                  key={d}
                  type="button"
                  aria-pressed={d === days}
                  onClick={() => setDays(d)}
                  className={cn(
                    'rounded-full px-3 py-1.5 text-sm font-medium ring-1 transition-colors',
                    d === days ? 'bg-primary text-primary-foreground ring-primary' : 'ring-border hover:bg-secondary',
                  )}
                >
                  {t('users.daysOption', { count: d })}
                </button>
              ))}
            </div>
          </div>
        )}

        <div className="grid gap-2">
          <Label htmlFor="stop-reason">{t('users.reason')}</Label>
          <Textarea id="stop-reason" value={reason} maxLength={500} onChange={(event) => setReason(event.target.value)} />
          <p className="text-muted-foreground text-xs">{t('users.reasonHint')}</p>
        </div>

        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>{t('action.cancel', { ns: 'common' })}</DialogClose>
          <Button type="submit" variant={kind === 'ban' ? 'destructive' : 'default'} disabled={!reason.trim()}>
            {t(kind === 'lock' ? 'users.confirmLock' : 'users.confirmBan')}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  )
}
