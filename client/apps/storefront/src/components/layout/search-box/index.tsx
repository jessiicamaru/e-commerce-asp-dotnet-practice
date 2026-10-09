import { useEffect, useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { FolderIcon, SearchIcon } from 'lucide-react'
import { cn } from 'cn'
import { ProductImage } from '@ecommerce/core/components/product/product-image'
import { Price } from '@ecommerce/core/components/shared/price'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@ecommerce/ui/input-group'
import { useSearchSuggestions } from '@ecommerce/core/hooks/product'

/** How long typing must pause before asking (research D4). */
export const SUGGEST_DELAY_MS = 200

type Option = { key: string; href: string }

/**
 * The search box, with suggestions while typing (specs/164, #376): a few products and categories under it after two
 * characters and a pause. A combobox in the ARIA sense - up and down move, Enter opens the highlighted option, Escape
 * closes - and Enter with nothing highlighted submits the form it sits in, which searches exactly as before.
 */
export function SearchBox({ initial }: { initial: string }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const listId = useId()
  const [value, setValue] = useState(initial)
  const [term, setTerm] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(-1)

  // One request per pause in typing, never one per key; the answer is keyed by its term, so a late one for an older
  // term is never shown over a newer one.
  useEffect(() => {
    const trimmed = value.trim()
    const timer = setTimeout(() => setTerm(trimmed.length >= 2 ? trimmed : ''), SUGGEST_DELAY_MS)
    return () => clearTimeout(timer)
  }, [value])

  const suggestions = useSearchSuggestions(term)
  const current = term !== '' && term === value.trim() ? suggestions.data : undefined
  const options: Option[] = [
    ...(current?.products ?? []).map((p) => ({ key: `p-${p.id}`, href: `/products/${p.id}` })),
    ...(current?.categories ?? []).map((c) => ({ key: `c-${c.id}`, href: `/?category=${c.id}` })),
  ]
  const shown = open && options.length > 0

  function choose(option: Option) {
    setOpen(false)
    setActive(-1)
    navigate(option.href)
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Escape') {
      setOpen(false)
      setActive(-1)
      return
    }
    if (!shown) return
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      const step = event.key === 'ArrowDown' ? 1 : -1
      // -1 is "nothing highlighted" - the typed text - and the keys cycle through it like through the options.
      setActive((index) => {
        const next = index + step
        return next >= options.length ? -1 : next < -1 ? options.length - 1 : next
      })
      return
    }
    if (event.key === 'Enter' && active >= 0 && active < options.length) {
      // A highlighted option is chosen; with none, Enter submits the form and searches as before.
      event.preventDefault()
      choose(options[active])
    }
  }

  const optionId = (index: number) => `${listId}-${index}`
  const products = current?.products ?? []
  const categories = current?.categories ?? []

  return (
    <div className="relative">
      <InputGroup className="bg-secondary/70 h-10 rounded-full border-0">
        <InputGroupAddon>
          <SearchIcon />
        </InputGroupAddon>
        <InputGroupInput
          name="q"
          type="search"
          role="combobox"
          aria-label={t('action.search')}
          aria-autocomplete="list"
          aria-expanded={shown}
          aria-controls={listId}
          aria-activedescendant={shown && active >= 0 ? optionId(active) : undefined}
          autoComplete="off"
          value={value}
          placeholder={t('searchPlaceholder')}
          onChange={(event) => {
            setValue(event.target.value)
            setOpen(true)
            setActive(-1)
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => setOpen(false)}
          onKeyDown={onKeyDown}
        />
      </InputGroup>

      {shown && (
        <ul
          id={listId}
          role="listbox"
          aria-label={t('search.suggestions')}
          className="bg-card ring-border/60 absolute inset-x-0 top-full z-50 mt-2 grid gap-0.5 overflow-hidden rounded-2xl p-1.5 shadow-lg ring-1"
        >
          {products.map((product, index) => (
            <li
              key={product.id}
              id={optionId(index)}
              role="option"
              aria-selected={active === index}
              // Not a blur: the input keeps focus until the click has chosen.
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => choose(options[index])}
              className={cn('flex cursor-pointer items-center gap-3 rounded-xl px-2 py-1.5', active === index && 'bg-accent')}
            >
              <span className="w-10 shrink-0">
                <ProductImage product={{ id: product.id, name: product.name, imageUrl: product.imageUrl }} thumb />
              </span>
              <span className="min-w-0 flex-1 truncate text-sm font-medium">{product.name}</span>
              <Price value={product.price} currency={product.currency} className="shrink-0 text-sm" />
            </li>
          ))}
          {categories.map((category, offset) => {
            const index = products.length + offset
            return (
              <li
                key={category.id}
                id={optionId(index)}
                role="option"
                aria-selected={active === index}
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => choose(options[index])}
                className={cn('flex cursor-pointer items-center gap-3 rounded-xl px-2 py-2 text-sm', active === index && 'bg-accent')}
              >
                <FolderIcon className="text-muted-foreground size-4 shrink-0" />
                <span className="min-w-0 flex-1 truncate">{category.name}</span>
                <span className="text-muted-foreground text-xs">{t('search.category')}</span>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
