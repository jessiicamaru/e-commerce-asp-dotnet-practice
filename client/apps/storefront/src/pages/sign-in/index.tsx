import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { SignInForm } from '@ecommerce/core/components/sign-in-form'

/**
 * Signing in to the storefront (specs/110). The two steps and every refusal are the shared form's (specs/136); this
 * page adds what only the storefront has - why the shopper was sent here (specs/126), "forgot your password?" and
 * "create an account" - and where they go afterwards.
 */
export function SignInPage() {
  const { t } = useTranslation('auth')
  const navigate = useNavigate()
  const location = useLocation()

  const arrival = location.state as { from?: string; reason?: string } | null
  // Why the shopper is here, when a page sent them (specs/126): pressing Add to cart, or opening the cart, signed out.
  const reason =
    arrival?.reason === 'cart'
      ? t('signIn.reasonAddToCart')
      : arrival?.reason === 'checkout'
        ? t('signIn.reasonCheckout')
        : arrival?.from?.startsWith('/cart')
          ? t('signIn.reasonCart')
          : null

  return (
    <SignInForm
      onSignedIn={() => navigate(arrival?.from ?? '/')}
      onSetupRequired={() => navigate('/account/two-factor')}
      description={reason}
      passwordAction={
        <Link to="/forgot-password" className="text-muted-foreground text-sm underline">
          {t('signIn.forgot')}
        </Link>
      }
      footer={
        <p className="text-muted-foreground mt-4 text-sm">
          {t('signIn.noAccount')}{' '}
          <Link to="/sign-up" className="underline">
            {t('signIn.createOne')}
          </Link>
          .
        </p>
      }
    />
  )
}
