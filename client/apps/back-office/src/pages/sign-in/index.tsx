import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'
import { SignInForm } from '@ecommerce/core/components/sign-in-form'

/**
 * Signing in to the back office (specs/136): the storefront's own form - password, then the code - with none of the
 * storefront's extras. Staff accounts are not made by sign-up, and a forgotten password is reset from the storefront.
 */
export function SignInPage() {
  const { t } = useTranslation('backOffice')
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-6 p-4">
      <p className="font-heading text-lg font-semibold">{t('name')}</p>
      <div className="w-full">
        {/* Staff without two-factor sign-in land on the guard, which says where to set it up. */}
        <SignInForm onSignedIn={() => navigate(from, { replace: true })} onSetupRequired={() => navigate('/', { replace: true })} />
      </div>
    </main>
  )
}
