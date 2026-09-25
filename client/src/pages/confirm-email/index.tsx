import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { ErrorMessage } from '@/components/shared/query-state'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuth } from '@/context/auth/useAuth'
import { Auth } from '@/services/auth'

type Outcome = 'working' | 'done' | 'invalid'

/**
 * Where the emailed link lands (specs/063): `/confirm-email?token=…`. It confirms at once - there is nothing
 * to type - and, when this browser is signed in, renews the session so the banner goes.
 *
 * The link works ONCE, so it is sent once: React's development double effect would otherwise use it and
 * then report the second, refused use as "invalid".
 */
export function ConfirmEmailPage() {
  const { t } = useTranslation('auth')
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const { user, refreshSession } = useAuth()
  const [outcome, setOutcome] = useState<Outcome>(token ? 'working' : 'invalid')
  const sent = useRef(false)
  const signedIn = user !== null

  useEffect(() => {
    if (!token || sent.current) return
    sent.current = true

    Auth.confirmEmail(token)
      .then(async () => {
        setOutcome('done')
        if (signedIn) await refreshSession()
      })
      .catch(() => setOutcome('invalid'))
  }, [token, signedIn, refreshSession])

  return (
    <Card className="mx-auto max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">{t('confirm.title')}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {outcome === 'working' && <p className="text-muted-foreground text-sm">{t('confirm.working')}</p>}
        {outcome === 'done' && (
          <Alert role="status">
            <AlertDescription>{t('confirm.done')}</AlertDescription>
          </Alert>
        )}
        {outcome === 'invalid' && <ErrorMessage>{t('confirm.invalid')}</ErrorMessage>}
        {outcome !== 'working' && (
          <Link to="/" className={buttonVariants({ variant: outcome === 'done' ? 'default' : 'outline' })}>
            {t('confirm.continue')}
          </Link>
        )}
      </CardContent>
    </Card>
  )
}
