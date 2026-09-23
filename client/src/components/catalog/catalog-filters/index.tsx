import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { SearchIcon } from 'lucide-react'
import { SearchableSelect } from '@/components/shared/searchable-select'
import { Button } from '@/components/ui/button'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import type { Category } from '@/services/category/types'
import type { SortBy } from '@/services/product/types'

const ALL_CATEGORIES = 'all'

const SORTS: SortBy[] = ['name_asc', 'name_desc', 'price_asc', 'price_desc']

/**
 * Search, category and sort. What they change lives in the URL, so a result can be shared.
 *
 * The category is a list you can type into, because it grows with the catalogue; the sort is four
 * fixed choices and stays a plain select - a search box over four items is friction, not help.
 */
export function CatalogFilters({
  searchTerm,
  categoryId,
  sortBy,
  categories,
  onChange,
}: {
  searchTerm: string
  categoryId: string
  sortBy: SortBy
  categories: Category[]
  onChange: (changes: Record<string, string>) => void
}) {
  const { t } = useTranslation('catalog')
  const [draft, setDraft] = useState(searchTerm)

  // base-ui renders the VALUE in the trigger unless it is told the labels, which is how the sort
  // dropdown came to read "name_asc" on screen.
  const sortItems: Record<string, string> = Object.fromEntries(SORTS.map((sort) => [sort, t(`sort.${sort}`)]))

  function search(event: FormEvent) {
    event.preventDefault()
    onChange({ q: draft.trim() })
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
    </form>
  )
}
