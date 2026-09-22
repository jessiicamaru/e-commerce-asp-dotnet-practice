import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { Category } from '@/services/category/types'
import type { SortBy } from '@/services/product/types'

const ALL_CATEGORIES = 'all'

const SORTS: SortBy[] = ['name_asc', 'name_desc', 'price_asc', 'price_desc']

/** Search, category and sort. What they change lives in the URL, so a result can be shared. */
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

  // base-ui renders the VALUE in the trigger unless it is told the labels, which is how the two
  // dropdowns came to read "all" and "name_asc" on screen.
  const categoryItems: Record<string, string> = {
    [ALL_CATEGORIES]: t('allCategories'),
    ...Object.fromEntries(categories.map((category) => [category.id, category.name])),
  }
  const sortItems: Record<string, string> = Object.fromEntries(SORTS.map((sort) => [sort, t(`sort.${sort}`)]))

  function search(event: FormEvent) {
    event.preventDefault()
    onChange({ q: draft.trim() })
  }

  return (
    <form onSubmit={search} className="mb-6 flex flex-wrap gap-2">
      <Input
        type="search"
        placeholder={t('searchPlaceholder')}
        className="min-w-56 flex-1"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
      />
      <Select
        items={categoryItems}
        value={categoryId || ALL_CATEGORIES}
        onValueChange={(value) => onChange({ category: !value || value === ALL_CATEGORIES ? '' : String(value) })}
      >
        <SelectTrigger className="w-52" aria-label={t('category')}>
          <SelectValue placeholder={t('allCategories')} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL_CATEGORIES}>{t('allCategories')}</SelectItem>
          {categories.map((category) => (
            <SelectItem key={category.id} value={category.id}>
              {category.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select items={sortItems} value={sortBy} onValueChange={(value) => value && onChange({ sort: String(value) })}>
        <SelectTrigger className="w-52" aria-label={t('sort.label')}>
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
      <Button type="submit">{t('action.search', { ns: 'common' })}</Button>
    </form>
  )
}
