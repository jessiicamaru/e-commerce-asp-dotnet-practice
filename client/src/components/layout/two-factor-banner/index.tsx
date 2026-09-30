import { useTranslation } from 'react-i18next'
import { Link, useLocation } from 'react-router-dom'
import { ShieldAlertIcon } from 'lucide-react'
import { buttonVariants } from '@/components/ui/button'
import { useAuth } from '@/context/auth/useAuth'
import { cn } from '@/utils/shared'

/**
 * "Set up two-factor sign-in" above every page while a staff member signed in without it (specs/110). For drawing
 * only: their session holds no staff role until they do, and every staff page and endpoint refuses it on its own.
 */
export function TwoFactorBanner() {
  const { t } = useTranslation('auth')
  const { user } = useAuth()
  const { pathname } = useLocation()

  if (!user?.twoFactorSetupRequired || pathname === '/account/two-factor') return null

  return (
    <div
      role="status"
      className="bg-card ring-border/60 mb-6 flex flex-wrap items-center justify-between gap-3 rounded-2xl px-4 py-3 text-sm ring-1"
    >
      <p className="flex items-center gap-2">
        <ShieldAlertIcon className="size-4" /> {t('twoFactor.banner')}
      </p>
      <Link to="/account/two-factor" className={cn(buttonVariants({ size: 'sm' }), 'rounded-full')}>
        {t('twoFactor.bannerAction')}
      </Link>
    </div>
  )
}
