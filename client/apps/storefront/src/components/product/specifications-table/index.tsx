import { useTranslation } from 'react-i18next'
import type { ProductSpecification } from '@ecommerce/core/services/product/types'

/**
 * A product's specifications as a table (specs/159, #366) - Brand: Sony, Sensor: Full-frame - in the reader's language:
 * the server has already translated each name and option and left each text as written, in the order the category
 * declares them. Nothing is drawn for a product without any.
 */
export function SpecificationsTable({ specifications }: { specifications: ProductSpecification[] | null | undefined }) {
  const { t } = useTranslation('catalog')

  if (!specifications?.length) return null

  return (
    <section className="bg-card ring-border/60 rounded-[2rem] p-5 ring-1">
      <h2 className="mb-3 text-lg font-semibold">{t('specifications.title')}</h2>
      <dl className="divide-border/60 grid divide-y text-sm">
        {specifications.map((specification) => (
          <div key={specification.specificationId} className="grid grid-cols-[minmax(8rem,1fr)_2fr] gap-3 py-2">
            <dt className="text-muted-foreground">{specification.name}</dt>
            <dd className="font-medium">{specification.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}
