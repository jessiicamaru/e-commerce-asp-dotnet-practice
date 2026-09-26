import { useState } from 'react'
import type { UseQueryResult } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { SearchIcon } from 'lucide-react'
import { LoadingRows } from '@/components/shared/query-state'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { useMyProducts, useProducts } from '@/hooks/product'
import type { ProductQuery } from '@/services/product/types'

type Chosen = Map<string, string>

interface PickerProps {
  chosen: Chosen
  onChange: (chosen: Chosen) => void
}

const queryFor = (search: string): ProductQuery => ({ searchTerm: search.trim() || undefined, pageSize: 8 })

/**
 * The products a voucher applies to (specs/070 research D3): a seller picks from their own listings, an
 * administrator from the whole catalogue - each asking only its own list. Chosen ones stay chosen while the
 * search changes. Nothing chosen means everything the voucher may touch, which the form says in words.
 */
export function ProductPicker({ mine, ...props }: PickerProps & { mine: boolean }) {
  return mine ? <OwnProducts {...props} /> : <CatalogProducts {...props} />
}

function OwnProducts(props: PickerProps) {
  const [search, setSearch] = useState('')
  return <PickerView {...props} search={search} onSearch={setSearch} products={useMyProducts(queryFor(search), true)} />
}

function CatalogProducts(props: PickerProps) {
  const [search, setSearch] = useState('')
  return <PickerView {...props} search={search} onSearch={setSearch} products={useProducts(queryFor(search))} />
}

function PickerView({
  chosen,
  onChange,
  search,
  onSearch,
  products,
}: PickerProps & {
  search: string
  onSearch: (search: string) => void
  products: UseQueryResult<{ items: { id: string; name: string }[] }>
}) {
  const { t } = useTranslation('vouchers')
  const items = products.data?.items ?? []

  function toggle(id: string, name: string, on: boolean) {
    const next = new Map(chosen)
    if (on) next.set(id, name)
    else next.delete(id)
    onChange(next)
  }

  return (
    <div className="grid gap-2">
      <div className="relative">
        <SearchIcon className="text-muted-foreground absolute top-1/2 left-3 size-4 -translate-y-1/2" />
        <Input
          aria-label={t('form.search')}
          placeholder={t('form.search')}
          className="h-9 rounded-xl pl-9"
          value={search}
          onChange={(event) => onSearch(event.target.value)}
        />
      </div>
      {products.isPending ? (
        <LoadingRows rows={2} />
      ) : (
        <ul className="grid max-h-44 gap-1 overflow-y-auto">
          {/* What is chosen first, even when the search no longer finds it. */}
          {[...chosen].filter(([id]) => !items.some((p) => p.id === id)).map(([id, name]) => (
            <Row key={id} id={id} name={name} checked onToggle={toggle} />
          ))}
          {items.map((product) => (
            <Row key={product.id} id={product.id} name={product.name} checked={chosen.has(product.id)} onToggle={toggle} />
          ))}
          {items.length === 0 && chosen.size === 0 && <li className="text-muted-foreground text-sm">{t('form.noProducts')}</li>}
        </ul>
      )}
    </div>
  )
}

function Row({ id, name, checked, onToggle }: { id: string; name: string; checked: boolean; onToggle: (id: string, name: string, on: boolean) => void }) {
  return (
    <li>
      <label className="hover:bg-secondary flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1 text-sm">
        <Checkbox checked={checked} onCheckedChange={(on) => onToggle(id, name, on)} />
        <span className="truncate">{name}</span>
      </label>
    </li>
  )
}
