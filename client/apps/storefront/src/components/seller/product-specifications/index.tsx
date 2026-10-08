import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { SaveIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { Button } from '@ecommerce/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@ecommerce/ui/card'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@ecommerce/ui/select'
import { useCategorySpecifications } from '@ecommerce/core/hooks/category'
import { useSetProductSpecifications } from '@ecommerce/core/hooks/product'
import type { Product, ProductSpecificationValue } from '@ecommerce/core/services/product/types'

const NONE = ''

/**
 * The product's specifications (specs/159, #366): one field per specification of its category and department - a
 * choice of the category's options, or a text as written - saved as one set. A field left empty is not sent, which
 * clears it. Saving changes to an approved product sends it back to review (specs/045), as the banner above says.
 *
 * <p>
 * Keyed by the page on the stored values, so a save that changes them remounts this card with the new ones.
 * </p>
 */
export function ProductSpecificationsCard({ product }: { product: Product }) {
  const { t } = useTranslation('seller')
  const specifications = useCategorySpecifications(product.categoryId)
  const save = useSetProductSpecifications(product.id)
  const stored = new Map((product.specifications ?? []).map((s) => [s.specificationId, s.optionId ?? s.text ?? '']))
  const [values, setValues] = useState<Record<string, string>>(() => Object.fromEntries(stored))

  const list = specifications.data ?? []
  if (list.length === 0) return null

  const set = (id: string, value: string) => setValues((previous) => ({ ...previous, [id]: value }))
  const changed = list.some((s) => (values[s.id] ?? '').trim() !== (stored.get(s.id) ?? ''))

  function submit(event: React.FormEvent) {
    event.preventDefault()
    const sent: ProductSpecificationValue[] = list.flatMap((s) => {
      const value = (values[s.id] ?? '').trim()
      if (!value) return []
      return [s.kind === 'Choice' ? { specificationId: s.id, optionId: value } : { specificationId: s.id, text: value }]
    })
    save
      .mutateAsync(sent)
      .then(() => toast.success(t('specifications.saved')))
      .catch(() => {})
  }

  return (
    <Card className="rounded-3xl">
      <CardHeader>
        <CardTitle>{t('specifications.title')}</CardTitle>
        <CardDescription>{t('specifications.hint')}</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
          {list.map((specification) => {
            const id = `specification-${specification.id}`
            return (
              <div key={specification.id} className="grid gap-2">
                <Label htmlFor={id}>{specification.name}</Label>
                {specification.kind === 'Choice' ? (
                  <Select
                    items={[{ value: NONE, label: t('specifications.none') },
                      ...specification.options.map((o) => ({ value: o.id, label: o.value }))]}
                    value={values[specification.id] ?? NONE}
                    onValueChange={(value) => set(specification.id, String(value ?? NONE))}
                  >
                    <SelectTrigger id={id} className="h-10! w-full rounded-xl" aria-label={specification.name}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={NONE}>{t('specifications.none')}</SelectItem>
                      {specification.options.map((option) => (
                        <SelectItem key={option.id} value={option.id}>
                          {option.value}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    id={id}
                    maxLength={200}
                    className="h-10 rounded-xl"
                    value={values[specification.id] ?? ''}
                    onChange={(event) => set(specification.id, event.target.value)}
                  />
                )}
              </div>
            )
          })}
          <div className="flex flex-wrap items-center gap-3 sm:col-span-2">
            <Button type="submit" className="rounded-full" disabled={!changed || save.isPending}>
              <SaveIcon /> {t('specifications.save')}
            </Button>
            {save.isError && <ServerError error={save.error} fallback={t('specifications.failed')} />}
          </div>
        </form>
      </CardContent>
    </Card>
  )
}
