import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Availability } from '@/components/product/availability'
import { ProductImage } from '@/components/product/product-image'
import { Card, CardContent } from '@/components/ui/card'
import { Price } from '@/components/shared/price'
import type { Product } from '@/services/product/types'

export function ProductCard({ product, categoryName }: { product: Product; categoryName?: string }) {
  const { t } = useTranslation('catalog')

  return (
    <Card className="overflow-hidden p-0 transition-shadow hover:shadow-md">
      <Link to={`/products/${product.id}`} className="block">
        <ProductImage product={product} />
      </Link>
      <CardContent className="flex flex-col gap-1 p-4">
        <Link to={`/products/${product.id}`} className="font-medium hover:underline">
          {product.name}
        </Link>
        {categoryName && <span className="text-muted-foreground text-xs">{categoryName}</span>}
        <span className="font-semibold">
          {product.priceVaries && (
            <span className="text-muted-foreground text-xs font-normal">{t('product.from')}</span>
          )}
          <Price value={product.price} currency={product.currency} />
        </span>
        <Availability value={product.availability} />
      </CardContent>
    </Card>
  )
}
