import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { DEFAULT_CURRENCY } from '@/config/money'
import { useCategories } from '@/hooks/category'
import { useCreateProduct } from '@/hooks/product'

/**
 * Listing a product (specs/028).
 *
 * <p>
 * One form, one submission, one sellable product. `POST /api/products` creates the first variant
 * and reuses the product's id for it (specs/020), so this is the smallest honest create. Adding a
 * second shape, a translation or a second currency happens afterwards, on the listing's own page.
 * </p>
 * <p>
 * <b>The price field is labelled with the shop's DEFAULT currency, not the one being browsed in.</b>
 * The server stores this number as the default currency's amount and validates it against that
 * currency; labelling it with the active currency would let somebody reading in dollars type 1999
 * and list a 1,999-dong camera with nothing to warn them.
 * </p>
 * <p>
 * The three consequences are stated on the form rather than discovered later: the text is stored in
 * one language, the price exists in one currency, and a new listing has no stock so it reads out of
 * stock until somebody stocks it.
 * </p>
 */
export function NewProductPage() {
  const { t, i18n } = useTranslation('seller')
  const navigate = useNavigate()
  const categories = useCategories()
  const create = useCreateProduct()

  const [form, setForm] = useState({ name: '', description: '', sku: '', price: '', categoryId: '' })
  const set = (field: keyof typeof form) => (event: { target: { value: string } }) =>
    setForm((previous) => ({ ...previous, [field]: event.target.value }))

  function submit(event: React.FormEvent) {
    event.preventDefault()
    create.mutate(
      {
        name: form.name.trim(),
        description: form.description.trim() || null,
        sku: form.sku.trim(),
        price: Number(form.price),
        categoryId: form.categoryId,
      },
      { onSuccess: (product) => navigate(`/shop/products/${product.id}`) },
    )
  }

  const languageName = i18n.language.startsWith('vi') ? 'Tiếng Việt' : 'English'

  return (
    <section className="mx-auto grid max-w-2xl gap-6">
      <header className="grid gap-1">
        <Link to="/shop" className="text-muted-foreground text-sm underline">
          {t('title')}
        </Link>
        <h1 className="text-2xl font-bold">{t('create.title')}</h1>
      </header>

      <form onSubmit={submit} className="bg-card ring-border/60 grid gap-4 rounded-3xl p-6 ring-1">
        <Field label={t('create.name')} htmlFor="name">
          <Input id="name" required value={form.name} onChange={set('name')} />
        </Field>

        <Field label={t('create.category')} htmlFor="categoryId">
          <select
            id="categoryId"
            required
            value={form.categoryId}
            onChange={set('categoryId')}
            className="border-input bg-background h-9 w-full rounded-full border px-4 text-sm"
          >
            <option value="">—</option>
            {(categories.data ?? []).map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </Field>

        <Field label={t('create.description')} htmlFor="description">
          <textarea
            id="description"
            rows={4}
            value={form.description}
            onChange={set('description')}
            className="border-input bg-background w-full rounded-2xl border px-4 py-2 text-sm"
          />
        </Field>

        <Field label={t('create.sku')} htmlFor="sku" hint={t('create.skuHint')}>
          <Input id="sku" required value={form.sku} onChange={set('sku')} />
        </Field>

        {/* DEFAULT_CURRENCY, deliberately - see the note on Product.create. */}
        <Field label={t('create.price', { currency: DEFAULT_CURRENCY })} htmlFor="price">
          <Input
            id="price"
            required
            inputMode="decimal"
            value={form.price}
            onChange={set('price')}
            aria-label={t('create.price', { currency: DEFAULT_CURRENCY })}
          />
        </Field>

        <div className="bg-secondary/50 grid gap-1.5 rounded-2xl p-4 text-sm">
          <p className="font-semibold">{t('create.consequences.title')}</p>
          <p className="text-muted-foreground">{t('create.consequences.language', { language: languageName })}</p>
          <p className="text-muted-foreground">{t('create.consequences.currency', { currency: DEFAULT_CURRENCY })}</p>
          <p className="text-muted-foreground">{t('create.consequences.stock')}</p>
        </div>

        <ServerError error={create.error} fallback={t('listing.loadFailed')} />

        <Button type="submit" className="rounded-full" disabled={create.isPending}>
          {create.isPending ? t('create.submitting') : t('create.submit')}
        </Button>
      </form>
    </section>
  )
}

function Field({
  label,
  htmlFor,
  hint,
  children,
}: {
  label: string
  htmlFor: string
  hint?: string
  children: React.ReactNode
}) {
  return (
    <div className="grid gap-1.5">
      <label htmlFor={htmlFor} className="text-sm font-semibold">
        {label}
      </label>
      {children}
      {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
    </div>
  )
}
