import { useState, type FormEvent } from 'react'
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

const SORTS: { value: SortBy; label: string }[] = [
  { value: 'name_asc', label: 'Name A–Z' },
  { value: 'name_desc', label: 'Name Z–A' },
  { value: 'price_asc', label: 'Price, low to high' },
  { value: 'price_desc', label: 'Price, high to low' },
]

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
  const [draft, setDraft] = useState(searchTerm)

  function search(event: FormEvent) {
    event.preventDefault()
    onChange({ q: draft.trim() })
  }

  return (
    <form onSubmit={search} className="mb-6 flex flex-wrap gap-2">
      <Input
        type="search"
        placeholder="Search by name or SKU"
        className="min-w-56 flex-1"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
      />
      <Select
        value={categoryId || ALL_CATEGORIES}
        onValueChange={(value) => onChange({ category: !value || value === ALL_CATEGORIES ? '' : String(value) })}
      >
        <SelectTrigger className="w-52" aria-label="Category">
          <SelectValue placeholder="All categories" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL_CATEGORIES}>All categories</SelectItem>
          {categories.map((category) => (
            <SelectItem key={category.id} value={category.id}>
              {category.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={sortBy} onValueChange={(value) => value && onChange({ sort: String(value) })}>
        <SelectTrigger className="w-52" aria-label="Sort">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {SORTS.map((sort) => (
            <SelectItem key={sort.value} value={sort.value}>
              {sort.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Button type="submit">Search</Button>
    </form>
  )
}
