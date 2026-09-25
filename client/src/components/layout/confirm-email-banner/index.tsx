import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/context/auth/useAuth'
import { Auth } from '@/services/auth'
import { tooManyAttempts } from '@/utils/shared'

/**
 * "Confirm your email address" above every page while the signed-in person's address is unconfirmed
 * (specs/063), with a way to have the link sent again. For drawing only: what an unconfirmed account may
 * not do - open a shop - the server refuses on its own.
 */
export function ConfirmEmailBanner() {
  const { t } = useTranslation('auth')
  const { user } = useAuth()
  const [sending, setSending] = useState(false)

  if (!user || user.emailConfirmed) return null

  const resend = async () => {
    setSending(true)
    try {
      await Auth.resendConfirmation()
      toast.success(t('confirm.resent'))
    } catch (caught) {
      toast.error(tooManyAttempts(t, caught) ?? t('confirm.resendFailed'))
    } finally {
      setSending(false)
    }
  }

  return (
    <div
      role="status"
      className="bg-card ring-border/60 mb-6 flex flex-wrap items-center justify-between gap-3 rounded-2xl px-4 py-3 text-sm ring-1"
    >
      <p>{t('confirm.banner', { email: user.email })}</p>
      <Button size="sm" variant="outline" className="rounded-full" disabled={sending} onClick={() => void resend()}>
        {sending ? t('confirm.resending') : t('confirm.resend')}
      </Button>
    </div>
  )
}
