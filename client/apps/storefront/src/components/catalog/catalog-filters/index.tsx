import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { SearchIcon } from 'lucide-react'
import { SearchableSelect } from '@/components/shared/searchable-select'
import { Button } from '@ecommerce/ui/button'
import { Checkbox } from '@ecommerce/ui/checkbox'
import { Input } from '@ecommerce/ui/input'
import { currentCurrency } from '@ecommerce/core/config/money'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@ecommerce/ui/input-group'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@ecommerce/ui/select'
import type { Category } from '@ecommerce/core/services/category/types'
import type { SortBy } from '@ecommerce/core/services/product/types'

const ALL_CATEGORIES = 'all'

const SORTS: SortBy[] = ['name_asc', 'name_desc', 'price_asc', 'price_desc']

/**
 * Search, category and sort, and since specs/109 a price range and "in stock only". What they change lives in the
 * URL, so a result can be shared.
 *
 * The category is a list you can type into, because it grows with the catalogue; the sort is four
 * fixed choices and stays a plain select - a search box over four items is friction, not help. The price range is
 * in the currency being browsed in, and applied with the search; a reversed range is said here rather than sent.
 */
export function CatalogFilters({
  searchTerm,
  categoryId,
  sortBy,
  minPrice = '',
  maxPrice = '',
  inStock = false,
  categories,
  onChange,
}: {
  searchTerm: string
  categoryId: string
  sortBy: SortBy
  minPrice?: string
  maxPrice?: string
  inStock?: boolean
  categories: Category[]
  onChange: (changes: Record<string, string>) => void
}) {
  const { t } = useTranslation('catalog')
  const [draft, setDraft] = useState(searchTerm)
  const [min, setMin] = useState(minPrice)
  const [max, setMax] = useState(maxPrice)
  const reversed = min.trim() !== '' && max.trim() !== '' && Number(min) > Number(max)

  // base-ui renders the VALUE in the trigger unless it is told the labels, which is how the sort
  // dropdown came to read "name_asc" on screen.
  const sortItems: Record<string, string> = Object.fromEntries(SORTS.map((sort) => [sort, t(`sort.${sort}`)]))

  function search(event: FormEvent) {
    event.preventDefault()
    if (reversed) return
    onChange({ q: draft.trim(), min: min.trim(), max: max.trim() })
  }

  return (
    <form onSubmit={search} className="mb-6 grid gap-2 sm:grid-cols-[1fr_14rem_12rem_auto]">
      <InputGroup className="bg-card h-10 rounded-xl">
        <InputGroupAddon>
          <SearchIcon />
        </InputGroupAddon>
        <InputGroupInput
          type="search"
          placeholder={t('searchPlaceholder')}
          aria-label={t('searchPlaceholder')}
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
        />
      </InputGroup>

      <SearchableSelect
        label={t('category')}
        className="bg-card"
        choices={[
          { value: ALL_CATEGORIES, label: t('allCategories') },
          ...categories.map((category) => ({ value: category.id, label: category.name })),
        ]}
        value={categoryId || ALL_CATEGORIES}
        onChange={(value) => onChange({ category: !value || value === ALL_CATEGORIES ? '' : value })}
      />

      <Select items={sortItems} value={sortBy} onValueChange={(value) => value && onChange({ sort: String(value) })}>
        <SelectTrigger className="bg-card h-10! w-full rounded-xl" aria-label={t('sort.label')}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {SORTS.map((sort) => (
            <SelectItem key={sort} value={sort}>
              {t(`sort.${sort}`)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Button type="submit" className="h-10 rounded-xl px-5">
        {t('action.search', { ns: 'common' })}
      </Button>

      <div className="flex flex-wrap items-center gap-2 sm:col-span-4">
        <span className="text-muted-foreground text-sm">{t('price.label', { currency: currentCurrency() })}</span>
        <Input
          type="number"
          min={0}
          inputMode="decimal"
          className="bg-card h-9 w-28 rounded-xl sm:w-36"
          aria-label={t('price.min')}
          placeholder={t('price.min')}
          value={min}
          onChange={(event) => setMin(event.target.value)}
        />
        <span aria-hidden>–</span>
        <Input
          type="number"
          min={0}
          inputMode="decimal"
          className="bg-card h-9 w-28 rounded-xl sm:w-36"
          aria-label={t('price.max')}
          placeholder={t('price.max')}
          value={max}
          onChange={(event) => setMax(event.target.value)}
        />
        {reversed && (
          <span className="text-destructive text-sm" role="alert">
            {t('price.reversed')}
          </span>
        )}
        <label className="ml-auto flex cursor-pointer items-center gap-2 text-sm">
          <Checkbox checked={inStock} onCheckedChange={(checked) => onChange({ stock: checked ? '1' : '' })} />
          {t('inStockOnly')}
        </label>
      </div>
    </form>
  )
}
