import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'
import { HeartIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/context/auth/useAuth'
import { useSavedIds, useToggleSaved } from '@/hooks/saved-product'
import { cn } from '@/utils/shared'

/**
 * Save for later (specs/075, #109): a heart, filled when the product is on the shopper's list. Signed out, it
 * sends them to sign in and back here - saving is theirs, so it needs to know who they are.
 */
export function SaveButton({ productId, className }: { productId: string; className?: string }) {
  const { t } = useTranslation('catalog')
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const ids = useSavedIds(!!user)
  const toggle = useToggleSaved()
  const saved = ids.data?.has(productId) ?? false

  return (
    <Button
      type="button"
      size="icon"
      variant="secondary"
      aria-pressed={saved}
      aria-label={saved ? t('saved.unsave') : t('saved.save')}
      title={saved ? t('saved.unsave') : t('saved.save')}
      disabled={toggle.isPending}
      className={cn('rounded-full shadow-sm', className)}
      onClick={(event) => {
        event.preventDefault()
        if (!user) {
          navigate('/sign-in', { state: { from: location.pathname } })
          return
        }
        toggle.mutate({ productId, saved })
      }}
    >
      <HeartIcon className={cn(saved && 'fill-rose-500 text-rose-500')} />
    </Button>
  )
}
