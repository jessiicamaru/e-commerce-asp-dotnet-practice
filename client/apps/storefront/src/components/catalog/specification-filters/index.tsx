import { useTranslation } from 'react-i18next'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@ecommerce/ui/select'
import { useCategorySpecifications } from '@ecommerce/core/hooks/category'

const ANY = ''

/**
 * Filters by what the chosen category's products are compared by (specs/159, #366): one select per choice
 * specification - Brand, Sensor - each "any" or one option. Text specifications are not filters: equal values are only
 * equal when they are options (research D2). Nothing is drawn until a category is chosen.
 *
 * <p>
 * The chosen options travel as one list; picking another option of the same specification replaces the first, so the
 * server's "every option chosen" is the whole meaning.
 * </p>
 */
export function SpecificationFilters({
  categoryId,
  optionIds,
  onChange,
}: {
  categoryId: string
  optionIds: string[]
  onChange: (optionIds: string[]) => void
}) {
  const { t } = useTranslation('catalog')
  const specifications = useCategorySpecifications(categoryId || null)
  const choices = (specifications.data ?? []).filter((s) => s.kind === 'Choice' && s.options.length > 0)

  if (choices.length === 0) return null

  return (
    <div className="mb-6 flex flex-wrap gap-2" aria-label={t('specifications.filters')} role="group">
      {choices.map((specification) => {
        const own = new Set(specification.options.map((o) => o.id))
        const chosen = optionIds.find((id) => own.has(id)) ?? ANY
        const choose = (value: unknown) => {
          const others = optionIds.filter((id) => !own.has(id))
          onChange(value ? [...others, String(value)] : others)
        }

        return (
          <Select
            key={specification.id}
            items={[{ value: ANY, label: t('specifications.any', { name: specification.name }) },
              ...specification.options.map((o) => ({ value: o.id, label: o.value }))]}
            value={chosen}
            onValueChange={choose}
          >
            <SelectTrigger className="bg-card h-9! min-w-40 rounded-xl" aria-label={specification.name}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>{t('specifications.any', { name: specification.name })}</SelectItem>
              {specification.options.map((option) => (
                <SelectItem key={option.id} value={option.id}>
                  {option.value}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )
      })}
    </div>
  )
}
