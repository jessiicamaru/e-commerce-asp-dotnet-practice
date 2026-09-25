import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ErrorMessage } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/context/auth/useAuth'
import { describeSignInFailure } from './refusal'

export function SignInPage() {
  const { t, i18n } = useTranslation('auth')
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      await signIn(email, password)
      navigate((location.state as { from?: string } | null)?.from ?? '/')
    } catch (caught) {
      setError(describeSignInFailure(t, i18n.language, caught))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="mx-auto max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">{t('signIn.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="flex flex-col gap-4">
          <div className="grid gap-1.5">
            <Label htmlFor="email">{t('signIn.email')}</Label>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <div className="flex items-baseline justify-between">
              <Label htmlFor="password">{t('signIn.password')}</Label>
              <Link to="/forgot-password" className="text-muted-foreground text-sm underline">
                {t('signIn.forgot')}
              </Link>
            </div>
            <Input
              id="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </div>
          {error && <ErrorMessage>{error}</ErrorMessage>}
          <Button type="submit" disabled={busy}>
            {busy ? t('signIn.submitting') : t('signIn.submit')}
          </Button>
        </form>
        <p className="text-muted-foreground mt-4 text-sm">
          {t('signIn.noAccount')}{' '}
          <Link to="/sign-up" className="underline">
            {t('signIn.createOne')}
          </Link>
          .
        </p>
      </CardContent>
    </Card>
  )
}
