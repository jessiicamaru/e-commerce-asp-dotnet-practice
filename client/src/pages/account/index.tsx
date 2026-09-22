import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useAuth } from '@/context/auth/useAuth'

export function AccountPage() {
  const { t } = useTranslation('auth')
  const { user } = useAuth()

  return (
    <section>
      <h1 className="mb-4 text-2xl font-bold">{t('account.title')}</h1>
      <p>
        {user?.firstName} {user?.lastName} · {user?.email}
      </p>
      <p className="text-muted-foreground mt-2 text-sm">{t('account.note')}</p>
      <p className="mt-4 flex gap-3 text-sm">
        <Link to="/addresses" className="underline">
          {t('account.addresses')}
        </Link>
        <Link to="/cart" className="underline">
          {t('account.cart')}
        </Link>
        <Link to="/orders" className="underline">
          {t('account.orders')}
        </Link>
      </p>
    </section>
  )
}
