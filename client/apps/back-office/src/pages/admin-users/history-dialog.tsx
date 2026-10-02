import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@ecommerce/ui/dialog'
import type { Account } from '@ecommerce/core/services/accounts/types'
import { PersonHistory } from './person-history'

/** A person's whole moderation history, paged (specs/100), opened from their row's menu. */
export function HistoryDialog({ account, onClose }: { account: Account | null; onClose: () => void }) {
  const { t } = useTranslation('admin')

  return (
    <Dialog open={account !== null} onOpenChange={(open) => !open && onClose()}>
      {account && (
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('users.history.dialogTitle', { email: account.email })}</DialogTitle>
            <DialogDescription>{t('users.history.dialogBody')}</DialogDescription>
          </DialogHeader>
          {/* Keyed on the person, so the page reached for one is not where the next one opens. */}
          <PagedHistory key={account.id} userId={account.id} />
        </DialogContent>
      )}
    </Dialog>
  )
}

function PagedHistory({ userId }: { userId: string }) {
  const [page, setPage] = useState(1)
  return <PersonHistory userId={userId} pageSize={10} page={page} onPage={setPage} />
}
