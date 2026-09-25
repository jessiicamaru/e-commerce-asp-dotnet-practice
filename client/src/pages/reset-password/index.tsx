import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError } from '@/config/axios'
import { ErrorMessage } from '@/components/shared/query-state'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Auth } from '@/services/auth'

/**
 * Where the emailed link lands (specs/061): `/reset-password?token=…`. A used, expired, replaced or
 * made-up token is one refusal on the server, and one sentence here - with the way to a new link.
 */
export function ResetPasswordPage() {
  const { t } = useTranslation('auth')
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [passwordError, setPasswordError] = useState<string | null>(null)
  const [linkDead, setLinkDead] = useState(token === '')
  const [done, setDone] = useState(false)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setPasswordError(null)
    if (password !== confirm) {
      setError(t('reset.mismatch'))
      return
    }

    setBusy(true)
    try {
      await Auth.resetPassword(token, password)
      setDone(true)
    } catch (caught) {
      const apiError = ApiError.from(caught)
      const fields = apiError.formErrors
      if (apiError.status === 400 && fields.token) {
        setLinkDead(true)
      } else if (apiError.status === 400 && fields.password) {
        // Registration's rules (#43), said by the server beside the field they are about.
        setPasswordError(fields.password)
      } else {
        setError(t('reset.failed'))
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="mx-auto max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">{t('reset.title')}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {done ? (
          <>
            <Alert role="status">
              <AlertDescription>{t('reset.done')}</AlertDescription>
            </Alert>
            <Link to="/sign-in" className={buttonVariants()}>
              {t('reset.signIn')}
            </Link>
          </>
        ) : linkDead ? (
          <>
            <ErrorMessage>{t('reset.invalidLink')}</ErrorMessage>
            <Link to="/forgot-password" className="text-sm underline">
              {t('reset.askAgain')}
            </Link>
          </>
        ) : (
          <form onSubmit={submit} className="flex flex-col gap-4">
            <p className="text-muted-foreground text-sm">{t('reset.hint')}</p>
            <div className="grid gap-1.5">
              <Label htmlFor="password">{t('reset.password')}</Label>
              <Input
                id="password"
                type="password"
                autoComplete="new-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
              {passwordError && <p className="text-destructive text-sm">{passwordError}</p>}
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="confirm">{t('reset.confirm')}</Label>
              <Input
                id="confirm"
                type="password"
                autoComplete="new-password"
                required
                value={confirm}
                onChange={(event) => setConfirm(event.target.value)}
              />
            </div>
            {error && <ErrorMessage>{error}</ErrorMessage>}
            <Button type="submit" disabled={busy}>
              {busy ? t('reset.submitting') : t('reset.submit')}
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  )
}
