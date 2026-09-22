import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'

/** In stock / out of stock, never a number: Catalog only ever knows that much (specs/004). */
export function Availability({ value }: { value: string }) {
  const { t } = useTranslation('catalog')

  return value === 'InStock' ? (
    <Badge variant="outline" className="border-emerald-600/40 text-emerald-700 dark:text-emerald-400">
      {t('product.inStock')}
    </Badge>
  ) : (
    <Badge variant="outline" className="text-muted-foreground">
      {t('product.outOfStock')}
    </Badge>
  )
}
