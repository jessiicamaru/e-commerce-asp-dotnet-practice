import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { PlusIcon, SearchIcon } from 'lucide-react'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { Button } from '@ecommerce/ui/button'
import { Input } from '@ecommerce/ui/input'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@ecommerce/ui/input-group'
import { Label } from '@ecommerce/ui/label'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { ApiError } from '@ecommerce/core/config/axios'
import { useCategoriesIn, useCategoryChanges } from '@ecommerce/core/hooks/category'
import type { Category } from '@ecommerce/core/services/category/types'
import { slugOf } from '@ecommerce/core/utils/shared'
import { CategoryRow } from './category-row'

/**
 * The shop's categories (specs/097): created, renamed and translated here rather than through the API. Vietnamese is the
 * category's own text, English a translation (specs/026); the address (slug) is fixed when it is created, so links to it
 * keep working. Deleting one that still has products is refused by the server, and the page says why in its words.
 *
 * <p>
 * Searched by name (either language) or address, twelve to a page (specs/133, #249). The list comes back whole from
 * the server, so both are done here - nothing narrower to ask for.
 * </p>
 */
export function AdminCategoriesPage() {
  const { t } = useTranslation('admin')
  const vi = useCategoriesIn('vi')
  const en = useCategoriesIn('en')
  const changes = useCategoryChanges()
  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [slugTouched, setSlugTouched] = useState(false)
  const [params, setParams] = useSearchParams()
  const search = params.get('q') ?? ''
  const page = Number(params.get('page') ?? '1') || 1

  const create = (event: FormEvent) => {
    event.preventDefault()
    changes.create
      .mutateAsync({ name: name.trim(), slug: (slugTouched ? slug : slugOf(name)).trim(), description: null })
      .then(() => {
        toast.success(t('categories.created', { name: name.trim() }))
        setName('')
        setSlug('')
        setSlugTouched(false)
      }, () => {})
  }

  const english = new Map<string, Category>((en.data ?? []).map((c) => [c.id, c]))
  const term = search.trim().toLowerCase()
  const found = (vi.data ?? []).filter(
    (c) =>
      !term ||
      c.name.toLowerCase().includes(term) ||
      c.slug.toLowerCase().includes(term) ||
      (english.get(c.id)?.name.toLowerCase().includes(term) ?? false),
  )
  const shown = found.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)
  const go = (next: { q?: string; page?: number }) => {
    const merged = new URLSearchParams()
    const q = next.q ?? search
    if (q) merged.set('q', q)
    if (next.page && next.page > 1) merged.set('page', String(next.page))
    setParams(merged)
  }
  const refusal = changes.create.error ? ApiError.from(changes.create.error).message : null

  return (
    <section className="grid gap-6">
      <PageTitle title={t('categories.title')} subtitle={t('categories.subtitle')} />

      <form onSubmit={create} className="bg-card ring-border/60 grid gap-3 rounded-3xl p-4 ring-1 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
        <div className="grid gap-1.5">
          <Label htmlFor="category-name">{t('categories.name')}</Label>
          <Input id="category-name" value={name} onChange={(event) => setName(event.target.value)} required maxLength={100} />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor="category-slug">{t('categories.slug')}</Label>
          <Input
            id="category-slug"
            value={slugTouched ? slug : slugOf(name)}
            onChange={(event) => {
              setSlugTouched(true)
              setSlug(event.target.value)
            }}
            maxLength={150}
          />
        </div>
        <Button type="submit" className="rounded-full" disabled={!name.trim() || changes.create.isPending}>
          <PlusIcon /> {t('categories.create')}
        </Button>
        {refusal && <p className="text-destructive text-sm sm:col-span-3">{refusal}</p>}
      </form>

      {vi.isError || en.isError ? (
        <ErrorMessage>{t('categories.loadFailed')}</ErrorMessage>
      ) : vi.isPending || en.isPending ? (
        <LoadingRows />
      ) : vi.data.length === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('categories.none')}</p>
      ) : (
        <>
          <InputGroup className="h-10 max-w-sm rounded-full">
            <InputGroupAddon>
              <SearchIcon />
            </InputGroupAddon>
            <InputGroupInput
              aria-label={t('categories.search')}
              placeholder={t('categories.search')}
              value={search}
              onChange={(event) => go({ q: event.target.value, page: 1 })}
            />
          </InputGroup>
          {found.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('categories.noneFound')}</p>
          ) : (
            <ul className="grid gap-3">
              {shown.map((category) => (
                <CategoryRow key={category.id} vi={category} en={english.get(category.id)} changes={changes} />
              ))}
            </ul>
          )}
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={found.length} onChange={(next) => go({ page: next })} />
        </>
      )}
    </section>
  )
}
