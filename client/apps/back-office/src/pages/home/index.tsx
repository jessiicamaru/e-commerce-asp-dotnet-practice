import { useTranslation } from 'react-i18next'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'

/** The back office's first page (specs/136): who is signed in, and as what. The console moves here in #277. */
export function HomePage() {
  const { t } = useTranslation('backOffice')
  const { user, isAdmin } = useAuth()

  return (
    <section className="grid gap-2">
      <h1 className="font-heading text-2xl font-semibold">{t('home.greeting', { name: user?.firstName ?? user?.email })}</h1>
      <p className="text-muted-foreground">{isAdmin ? t('home.asAdmin') : t('home.asModerator')}</p>
    </section>
  )
}
