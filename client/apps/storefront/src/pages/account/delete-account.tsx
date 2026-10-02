import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@ecommerce/ui/alert-dialog'
import { Button } from '@ecommerce/ui/button'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { ApiError } from '@ecommerce/core/config/axios'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { useDeleteMe } from '@ecommerce/core/hooks/me'
import { deletionRefusal } from '@ecommerce/core/utils/account'
import { tooManyAttempts } from '@ecommerce/core/utils/shared'

/**
 * "Delete my account" (specs/112): what goes and what the shop keeps, said before the password is asked for; a
 * confirmation; then signed out. A refusal - business still open, a staff account - is listed in the reader's words.
 */
export function DeleteAccountCard() {
  const { t } = useTranslation('auth')
  const { signOut } = useAuth()
  const navigate = useNavigate()
  const remove = useDeleteMe()
  const [password, setPassword] = useState('')
  const [confirming, setConfirming] = useState(false)

  const ask = (event: FormEvent) => {
    event.preventDefault()
    setConfirming(true)
  }

  const confirm = () => {
    setConfirming(false)
    // mutateAsync, not a callback to mutate(): signing out unmounts this page before a callback would run (specs/080).
    void remove.mutateAsync(password).then(
      async () => {
        toast.success(t('account.deleted'))
        await signOut()
        navigate('/', { replace: true })
      },
      () => undefined,
    )
  }

  const refusal = remove.error ? deletionRefusal(remove.error, t) : null
  const failure = remove.error ? ApiError.from(remove.error) : null
  const wrongPassword = failure?.status === 400 ? failure.fieldErrors.Password : undefined
  const wait = remove.error ? tooManyAttempts(t, remove.error) : null

  return (
    <form onSubmit={ask} className="bg-card ring-destructive/30 grid gap-4 rounded-3xl p-6 ring-1">
      <div>
        <h2 className="text-destructive font-semibold">{t('account.deleteTitle')}</h2>
        <p className="text-muted-foreground text-sm">{t('account.deleteHint')}</p>
        <p className="text-muted-foreground mt-1 text-sm">{t('account.deleteShopHint')}</p>
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="deletePassword">{t('account.deletePassword')}</Label>
        <Input
          id="deletePassword"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          aria-invalid={wrongPassword ? true : undefined}
          onChange={(event) => setPassword(event.target.value)}
        />
        {wrongPassword && <p className="text-destructive text-sm">{wrongPassword}</p>}
      </div>
      {refusal && (
        <div role="alert" className="text-destructive grid gap-1 text-sm">
          <p className="font-medium">{t('account.deleteRefused')}</p>
          <ul className="list-disc pl-5">
            {refusal.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        </div>
      )}
      {wait && <p role="alert" className="text-destructive text-sm">{wait}</p>}
      {remove.error && !refusal && !wrongPassword && !wait && (
        <ServerError error={remove.error} fallback={t('account.deleteFailed')} />
      )}
      <Button type="submit" variant="destructive" className="justify-self-start rounded-full" disabled={remove.isPending}>
        {remove.isPending ? t('account.deleting') : t('account.deleteStart')}
      </Button>

      <AlertDialog open={confirming} onOpenChange={setConfirming}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('account.deleteConfirmTitle')}</AlertDialogTitle>
            <AlertDialogDescription>{t('account.deleteConfirmBody')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('account.deleteCancel')}</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirm}>
              {t('account.deleteConfirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </form>
  )
}
