import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ErrorMessage } from '@/components/shared/query-state'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Auth } from '@/services/auth'
import { tooManyAttempts } from '@/utils/shared'

/**
 * "I forgot my password" (specs/061). Once asked, it says the same thing for any address: the server does
 * not say whether the email has an account (#28), so neither may the page.
 */
export function ForgotPasswordPage() {
  const { t } = useTranslation('auth')
  const [email, setEmail] = useState('')
  const [sentTo, setSentTo] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      await Auth.forgotPassword(email)
      setSentTo(email)
    } catch (caught) {
      setError(tooManyAttempts(t, caught) ?? t('forgot.failed'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="mx-auto max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">{t('forgot.title')}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {sentTo ? (
          <Alert role="status">
            <AlertDescription>{t('forgot.sent', { email: sentTo })}</AlertDescription>
          </Alert>
        ) : (
          <form onSubmit={submit} className="flex flex-col gap-4">
            <p className="text-muted-foreground text-sm">{t('forgot.hint')}</p>
            <div className="grid gap-1.5">
              <Label htmlFor="email">{t('forgot.email')}</Label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </div>
            {error && <ErrorMessage>{error}</ErrorMessage>}
            <Button type="submit" disabled={busy}>
              {busy ? t('forgot.submitting') : t('forgot.submit')}
            </Button>
          </form>
        )}
        <Link to="/sign-in" className="text-muted-foreground text-sm underline">
          {t('forgot.back')}
        </Link>
      </CardContent>
    </Card>
  )
}
