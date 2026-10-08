import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { SaveIcon } from 'lucide-react'
import { toast } from 'sonner'
import { SearchableSelect } from '@/components/shared/searchable-select'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { Button } from '@ecommerce/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@ecommerce/ui/card'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { Textarea } from '@ecommerce/ui/textarea'
import { useCategories } from '@ecommerce/core/hooks/category'
import { categoryChoices } from '@ecommerce/core/utils/category'
import { useUpdateProductDetails } from '@ecommerce/core/hooks/product'
import type { Product } from '@ecommerce/core/services/product/types'

/**
 * The product's original name and description, and its category (specs/124, #240) - what was typed when it was
 * listed, and what every reader whose language has no text of its own sees. Until this nothing could change them.
 *
 * <p>
 * Saving changes to an approved product sends it back to review (specs/045) - the banner above says so; the category
 * too, since it decides who finds the product. The server decides that, and whether this seller may write at all.
 * </p>
 */
export function ProductDetailsCard({ product }: { product: Product }) {
  const { t } = useTranslation('seller')
  const categories = useCategories()
  const save = useUpdateProductDetails(product.id)
  const original = product.original ?? { name: product.name, description: product.description }
  const [form, setForm] = useState({
    name: original.name,
    description: original.description ?? '',
    categoryId: product.categoryId,
  })

  const changed =
    form.name.trim() !== original.name ||
    (form.description.trim() || null) !== (original.description ?? null) ||
    form.categoryId !== product.categoryId

  function submit(event: React.FormEvent) {
    event.preventDefault()
    // mutateAsync, not mutate's onSuccess: the page keys this card by the stored text, so a save replaces it before a
    // callback handed to mutate() would run (CLAUDE.md, specs/080) - the toast was lost exactly that way.
    save
      .mutateAsync({ name: form.name.trim(), description: form.description.trim() || null, categoryId: form.categoryId })
      .then(() => toast.success(t('details.saved')))
      .catch(() => {})
  }

  return (
    <Card className="rounded-3xl">
      <CardHeader>
        <CardTitle>{t('details.title')}</CardTitle>
        <CardDescription>{t('details.hint')}</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="details-name">{t('create.name')}</Label>
            <Input
              id="details-name"
              required
              maxLength={200}
              className="h-10 rounded-xl"
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="details-category">{t('create.category')}</Label>
            <SearchableSelect
              id="details-category"
              required
              placeholder={t('create.categoryPlaceholder')}
              choices={categoryChoices(categories.data ?? [])}
              value={form.categoryId || null}
              onChange={(value) => setForm({ ...form, categoryId: value ?? '' })}
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="details-description">{t('create.description')}</Label>
            <Textarea
              id="details-description"
              rows={4}
              maxLength={2000}
              className="rounded-xl"
              value={form.description}
              onChange={(event) => setForm({ ...form, description: event.target.value })}
            />
          </div>
          <ServerError error={save.error} fallback={t('listing.loadFailed')} />
          <Button type="submit" className="w-fit rounded-full" disabled={!changed || !form.name.trim() || save.isPending}>
            <SaveIcon /> {t('details.save')}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
