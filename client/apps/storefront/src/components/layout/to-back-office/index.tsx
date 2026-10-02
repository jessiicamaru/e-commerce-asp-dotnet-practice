import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router-dom'
import { consoleAddress } from '@ecommerce/core/config/apps'

/**
 * An address of the console from before it moved (specs/137): the browser goes to the same page in the back office -
 * another application, so a full navigation, never an in-app route.
 */
export function ToBackOffice() {
  const { t } = useTranslation()
  const { pathname, search, hash } = useLocation()
  const target = consoleAddress(pathname + search + hash)

  useEffect(() => {
    if (target) window.location.replace(target)
  }, [target])

  return <p className="text-muted-foreground">{t('checkingSession')}</p>
}
