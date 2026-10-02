import { useState, type FormEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@ecommerce/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@ecommerce/ui/card'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { ErrorMessage } from '@ecommerce/core/components/query-state'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { describeCodeFailure, describeSignInFailure } from './refusal'

export interface SignInFormProps {
  /** Signed in: where the app goes next is the app's business. */
  onSignedIn(): void
  /** Staff without two-factor sign-in (specs/110): signed in, with no staff role until they set it up. */
  onSetupRequired(): void
  /** Under the title of the first step - the storefront says why the person was sent here (specs/126). */
  description?: ReactNode
  /** Beside the password's label - the storefront's "forgot your password?". */
  passwordAction?: ReactNode
  /** Under the first step - the storefront's "create an account". */
  footer?: ReactNode
  /** A challenge already in hand - a handoff redeemed (specs/140): the form opens at the code step. */
  initialChallenge?: string
}

/**
 * Signing in, in one step or two (specs/110), drawn the same in the storefront and the back office (specs/136): with
 * two-factor sign-in on, the right password leads to the code from the authenticator app - or one recovery code - and
 * only that signs the person in. Every refusal is worded here once, so the two apps cannot disagree about one.
 */
export function SignInForm({ onSignedIn, onSetupRequired, description, passwordAction, footer, initialChallenge }: SignInFormProps) {
  const { t, i18n } = useTranslation('auth')
  const { signIn, completeSignIn } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [challenge, setChallenge] = useState<string | null>(initialChallenge ?? null)
  const [code, setCode] = useState('')
  const [byRecoveryCode, setByRecoveryCode] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      const step = await signIn(email, password)
      if ('challenge' in step) {
        setChallenge(step.challenge)
      } else if (step.setupRequired) {
        onSetupRequired()
      } else {
        onSignedIn()
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
      onSignedIn()
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
        {description && <CardDescription>{description}</CardDescription>}
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
              {passwordAction}
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
        {footer}
      </CardContent>
    </Card>
  )
}
