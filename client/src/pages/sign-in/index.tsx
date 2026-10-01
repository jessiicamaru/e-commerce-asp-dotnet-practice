import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ErrorMessage } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/context/auth/useAuth'
import { describeCodeFailure, describeSignInFailure } from './refusal'

/**
 * Signing in, in one step or two (specs/110): with two-factor sign-in on, the right password leads to the code from
 * the authenticator app - or one recovery code - and only that signs the person in. Staff without it are sent to set
 * it up; until they do, the server gives them no staff role.
 */
export function SignInPage() {
  const { t, i18n } = useTranslation('auth')
  const { signIn, completeSignIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [challenge, setChallenge] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [byRecoveryCode, setByRecoveryCode] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const arrival = location.state as { from?: string; reason?: string } | null
  const onward = () => navigate(arrival?.from ?? '/')
  // Why the shopper is here, when a page sent them (specs/126): pressing Add to cart, or opening the cart, signed out.
  const reason =
    arrival?.reason === 'cart' ? t('signIn.reasonAddToCart') : arrival?.from?.startsWith('/cart') ? t('signIn.reasonCart') : null

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      const step = await signIn(email, password)
      if ('challenge' in step) {
        setChallenge(step.challenge)
      } else if (step.setupRequired) {
        navigate('/account/two-factor')
      } else {
        onward()
      }
    } catch (caught) {
      setError(describeSignInFailure(t, i18n.language, caught))
    } finally {
      setBusy(false)
    }
  }

  async function submitCode(event: FormEvent) {
    event.preventDefault()
    if (!challenge) return
    setBusy(true)
    setError(null)

    try {
      await completeSignIn(challenge, byRecoveryCode ? { recoveryCode: code.trim() } : { code: code.replace(/\s/g, '') })
      onward()
    } catch (caught) {
      const { message, restart } = describeCodeFailure(t, caught)
      // A dead challenge - too old, or five wrong codes - means starting again from the password.
      if (restart) {
        setChallenge(null)
        setPassword('')
      }
      setCode('')
      setError(message)
    } finally {
      setBusy(false)
    }
  }

  if (challenge) {
    return (
      <Card className="mx-auto max-w-md">
        <CardHeader>
          <CardTitle className="text-xl">{t('twoFactorStep.title')}</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={submitCode} className="flex flex-col gap-4">
            <p className="text-muted-foreground text-sm">
              {byRecoveryCode ? t('twoFactorStep.recoveryHint') : t('twoFactorStep.hint')}
            </p>
            <div className="grid gap-1.5">
              <Label htmlFor="code">{byRecoveryCode ? t('twoFactorStep.recoveryCode') : t('twoFactorStep.code')}</Label>
              <Input
                id="code"
                autoFocus
                required
                autoComplete="one-time-code"
                inputMode={byRecoveryCode ? 'text' : 'numeric'}
                maxLength={byRecoveryCode ? 14 : 7}
                value={code}
                onChange={(event) => setCode(event.target.value)}
              />
            </div>
            {error && <ErrorMessage>{error}</ErrorMessage>}
            <Button type="submit" disabled={busy || !code.trim()}>
              {busy ? t('signIn.submitting') : t('twoFactorStep.submit')}
            </Button>
            <button
              type="button"
              className="text-muted-foreground text-sm underline"
              onClick={() => {
                setByRecoveryCode((value) => !value)
                setCode('')
                setError(null)
              }}
            >
              {byRecoveryCode ? t('twoFactorStep.useApp') : t('twoFactorStep.useRecovery')}
            </button>
          </form>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card className="mx-auto max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">{t('signIn.title')}</CardTitle>
        {reason && <CardDescription>{reason}</CardDescription>}
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
