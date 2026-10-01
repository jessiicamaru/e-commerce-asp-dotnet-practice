import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import QRCode from 'qrcode'
import { ShieldCheckIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/context/auth/useAuth'
import { useMyTwoFactor, useTwoFactorMoves } from '@/hooks/me'
import type { TwoFactorSetup } from '@/services/auth/types'
import { RecoveryCodeList } from './recovery-codes'

/**
 * Two-factor sign-in with an authenticator app (specs/110; how it works: docs/features/auth/totp-two-factor.md).
 *
 * Setting up: a QR code (and the secret as text, for typing), a code from the app to prove it saved the right one,
 * then ten recovery codes shown once. Staff are sent here when they sign in without it; their staff pages come back
 * once it is on - the renewed session carries the roles the server withheld. Staff cannot turn it off.
 */
export function AccountTwoFactorPage() {
  const { t } = useTranslation('auth')
  const { user, refreshSession } = useAuth()
  const status = useMyTwoFactor()
  const moves = useTwoFactorMoves(refreshSession)
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null)
  const [codes, setCodes] = useState<string[] | null>(null)

  if (status.isError) return <ErrorMessage>{t('twoFactor.loadFailed')}</ErrorMessage>
  if (status.isPending || !status.data) return <LoadingRows rows={3} />

  // The codes of a confirmation or a new set - shown until the person says they kept them.
  if (codes) {
    return (
      <Card className="max-w-xl">
        <CardHeader>
          <CardTitle>{t('twoFactor.codesTitle')}</CardTitle>
          <CardDescription>{t('twoFactor.codesHint')}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4">
          <RecoveryCodeList codes={codes} />
          <Button onClick={() => setCodes(null)}>{t('twoFactor.codesKept')}</Button>
        </CardContent>
      </Card>
    )
  }

  const { enabled, enabledAt, recoveryCodesLeft, required } = status.data

  return (
    <section className="grid max-w-xl gap-6">
      <h1 className="flex items-center gap-2 text-2xl font-bold tracking-tight">
        <ShieldCheckIcon className="size-6" /> {t('twoFactor.title')}
      </h1>
      {user?.twoFactorSetupRequired && !enabled && (
        <p role="alert" className="bg-card ring-border/60 rounded-2xl px-4 py-3 text-sm ring-1">
          {t('twoFactor.staffMust')}
        </p>
      )}

      {enabled ? (
        <>
          <Card>
            <CardHeader>
              <CardTitle>{t('twoFactor.on')}</CardTitle>
              <CardDescription>
                {t('twoFactor.onSince', { at: enabledAt ? new Date(enabledAt).toLocaleDateString() : '' })} ·{' '}
                {t('twoFactor.codesLeft', { count: recoveryCodesLeft })}
              </CardDescription>
            </CardHeader>
          </Card>
          <CodeForm
            title={t('twoFactor.newCodes')}
            hint={t('twoFactor.newCodesHint')}
            action={t('twoFactor.newCodesAction')}
            busy={moves.newCodes.isPending}
            error={moves.newCodes.error}
            onCode={(code) => moves.newCodes.mutateAsync(code).then(setCodes, () => {})}
          />
          {required ? (
            <p className="text-muted-foreground text-sm">{t('twoFactor.staffKeep')}</p>
          ) : (
            <TurnOff busy={moves.turnOff.isPending} error={moves.turnOff.error}
              onTurnOff={(password, code) =>
                moves.turnOff.mutateAsync({ password, code }).then(() => toast.success(t('twoFactor.turnedOff')), () => {})
              }
            />
          )}
        </>
      ) : setup ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('twoFactor.scanTitle')}</CardTitle>
            <CardDescription>{t('twoFactor.scanHint')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            <QrImage uri={setup.uri} label={t('twoFactor.qrAlt')} />
            <div className="grid gap-1">
              <span className="text-muted-foreground text-xs">{t('twoFactor.typeInstead')}</span>
              <code className="bg-muted rounded-lg px-3 py-2 font-mono text-sm break-all" data-testid="two-factor-secret">
                {setup.secret.match(/.{1,4}/g)?.join(' ')}
              </code>
            </div>
            <CodeForm
              title={t('twoFactor.confirmTitle')}
              action={t('twoFactor.confirmAction')}
              busy={moves.confirm.isPending}
              error={moves.confirm.error}
              onCode={(code) =>
                moves.confirm.mutateAsync(code).then((recovery) => {
                  toast.success(t('twoFactor.turnedOn'))
                  setSetup(null)
                  setCodes(recovery)
                }, () => {})
              }
            />
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>{t('twoFactor.off')}</CardTitle>
            <CardDescription>{t('twoFactor.why')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3">
            <ServerError error={moves.setUp.error} fallback={t('twoFactor.loadFailed')} />
            <Button disabled={moves.setUp.isPending} onClick={() => moves.setUp.mutateAsync().then(setSetup, () => {})}>
              {t('twoFactor.start')}
            </Button>
          </CardContent>
        </Card>
      )}

      <Link to="/account" className="text-muted-foreground text-sm underline">
        {t('twoFactor.back')}
      </Link>
    </section>
  )
}

/** The otpauth:// URI drawn as a QR code, in the browser - the secret never leaves this page for an image service. */
function QrImage({ uri, label }: { uri: string; label: string }) {
  const [src, setSrc] = useState<string | null>(null)

  useEffect(() => {
    let current = true
    QRCode.toDataURL(uri, { margin: 1, width: 220 }).then((url) => current && setSrc(url), () => current && setSrc(null))
    return () => {
      current = false
    }
  }, [uri])

  return src ? <img src={src} alt={label} width={220} height={220} className="justify-self-center rounded-xl bg-white p-2" /> : null
}

function CodeForm({ title, hint, action, busy, error, onCode }: {
  title: string
  hint?: string
  action: string
  busy: boolean
  error: unknown
  onCode: (code: string) => void
}) {
  const { t } = useTranslation('auth')
  const [code, setCode] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    onCode(code.replace(/\s/g, ''))
    setCode('')
  }

  return (
    <form onSubmit={submit} className="grid gap-2">
      <Label htmlFor={`code-${action}`} className="font-semibold">{title}</Label>
      {hint && <p className="text-muted-foreground text-sm">{hint}</p>}
      <div className="flex gap-2">
        <Input
          id={`code-${action}`}
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={7}
          placeholder="123456"
          value={code}
          onChange={(event) => setCode(event.target.value)}
        />
        <Button type="submit" disabled={busy || code.replace(/\s/g, '').length !== 6}>{action}</Button>
      </div>
      <ServerError error={error} fallback={t('twoFactorStep.wrong')} />
    </form>
  )
}

function TurnOff({ busy, error, onTurnOff }: { busy: boolean; error: unknown; onTurnOff: (password: string, code: string) => void }) {
  const { t } = useTranslation('auth')
  const [password, setPassword] = useState('')
  const [code, setCode] = useState('')

  return (
    <form
      className="grid gap-2"
      onSubmit={(event) => {
        event.preventDefault()
        onTurnOff(password, code.replace(/\s/g, ''))
      }}
    >
      <span className="font-semibold">{t('twoFactor.turnOffTitle')}</span>
      <Label htmlFor="off-password">{t('signIn.password')}</Label>
      <Input id="off-password" type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      <Label htmlFor="off-code">{t('twoFactorStep.code')}</Label>
      <Input id="off-code" inputMode="numeric" autoComplete="one-time-code" maxLength={7} value={code} onChange={(e) => setCode(e.target.value)} />
      <ServerError error={error} fallback={t('twoFactorStep.wrong')} />
      <Button type="submit" variant="outline" className="text-destructive justify-self-start" disabled={busy || !password || !code}>
        {t('twoFactor.turnOff')}
      </Button>
    </form>
  )
}
