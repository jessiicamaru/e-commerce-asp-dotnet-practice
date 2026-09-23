import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { ChevronRightIcon, InfoIcon } from 'lucide-react'
import { SearchableSelect } from '@/components/shared/searchable-select'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { InputGroup, InputGroupAddon, InputGroupInput, InputGroupText } from '@/components/ui/input-group'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { DEFAULT_CURRENCY } from '@/config/money'
import { useCategories } from '@/hooks/category'
import { useCreateProduct } from '@/hooks/product'

/**
 * Listing a product (specs/028).
 *
 * <p>
 * One form, one submission, one sellable product. `POST /api/products` creates the first variant
 * and reuses the product's id for it (specs/020), so this is the smallest honest create. A second
 * shape, a translation, a second currency and a photograph happen afterwards, on the listing's page.
 * </p>
 * <p>
 * <b>The price field is labelled with the shop's DEFAULT currency, not the one being browsed in.</b>
 * The server stores this number as the default currency's amount and validates it against that
 * currency; labelling it with the active currency would let somebody reading in dollars type 1999
 * and list a 1,999-dong camera with nothing to warn them.
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
  const priceLabel = t('create.price', { currency: DEFAULT_CURRENCY })

  return (
    <section className="grid gap-6">
      <nav className="text-muted-foreground flex items-center gap-1 text-sm">
        <Link to="/shop/products" className="hover:text-foreground">
          {t('menu.products')}
        </Link>
        <ChevronRightIcon className="size-4" />
        <span className="text-foreground">{t('create.title')}</span>
      </nav>

      <h1 className="text-2xl font-bold tracking-tight">{t('create.title')}</h1>

      <form onSubmit={submit} className="grid items-start gap-6 lg:grid-cols-[1fr_20rem]">
        <Card className="rounded-3xl">
          <CardHeader>
            <CardTitle>{t('create.details')}</CardTitle>
            <CardDescription>{t('create.detailsHint')}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-5">
            <Field label={t('create.name')} htmlFor="name">
              <Input id="name" required className="h-10 rounded-xl" value={form.name} onChange={set('name')} />
            </Field>

            <Field label={t('create.category')} htmlFor="categoryId">
              <SearchableSelect
                id="categoryId"
                required
                placeholder={t('create.categoryPlaceholder')}
                choices={(categories.data ?? []).map((category) => ({ value: category.id, label: category.name }))}
                value={form.categoryId || null}
                onChange={(value) => setForm((previous) => ({ ...previous, categoryId: value ?? '' }))}
              />
            </Field>

            <Field label={t('create.description')} htmlFor="description">
              <Textarea id="description" rows={5} className="rounded-xl" value={form.description} onChange={set('description')} />
            </Field>

            <div className="grid gap-5 sm:grid-cols-2">
              <Field label={t('create.sku')} htmlFor="sku" hint={t('create.skuHint')}>
                <Input id="sku" required className="h-10 rounded-xl font-mono" value={form.sku} onChange={set('sku')} />
              </Field>

              {/* DEFAULT_CURRENCY, deliberately - see the note on Product.create. */}
              <Field label={priceLabel} htmlFor="price">
                <InputGroup className="h-10 rounded-xl">
                  <InputGroupInput
                    id="price"
                    required
                    inputMode="decimal"
                    value={form.price}
                    onChange={set('price')}
                    aria-label={priceLabel}
                  />
                  <InputGroupAddon align="inline-end">
                    <InputGroupText>{DEFAULT_CURRENCY}</InputGroupText>
                  </InputGroupAddon>
                </InputGroup>
              </Field>
            </div>

            <ServerError error={create.error} fallback={t('listing.loadFailed')} />
          </CardContent>
        </Card>

        <div className="grid gap-4 lg:sticky lg:top-28">
          <Card className="bg-secondary/50 rounded-3xl">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <InfoIcon className="size-4.5" /> {t('create.consequences.title')}
              </CardTitle>
            </CardHeader>
            <CardContent className="text-muted-foreground grid gap-2 text-sm">
              <p>{t('create.consequences.language', { language: languageName })}</p>
              <p>{t('create.consequences.currency', { currency: DEFAULT_CURRENCY })}</p>
              <p>{t('create.consequences.stock')}</p>
            </CardContent>
          </Card>

          <Button type="submit" className="h-11 rounded-full text-base font-semibold" disabled={create.isPending}>
            {create.isPending ? t('create.submitting') : t('create.submit')}
          </Button>
        </div>
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
    <div className="grid content-start gap-2">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
    </div>
  )
}
