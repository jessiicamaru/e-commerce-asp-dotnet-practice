import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PlusIcon, XIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ServerError } from '@ecommerce/core/components/shared/server-error'
import { Button } from '@ecommerce/ui/button'
import { Input } from '@ecommerce/ui/input'
import { InputGroup, InputGroupAddon, InputGroupInput, InputGroupText } from '@ecommerce/ui/input-group'
import { Label } from '@ecommerce/ui/label'
import { DEFAULT_CURRENCY } from '@ecommerce/core/config/money'
import { useAddVariant } from '@ecommerce/core/hooks/product'
import type { Product } from '@ecommerce/core/services/product/types'

/**
 * Another shape of the product - a kit, a colour (specs/020; specs/124, #240). The form the new-product page promised
 * and nothing offered.
 *
 * <p>
 * The options start from the names the product's variants already use (Kit, Colour), so a second shape is described
 * the way the first was; the price is the shop's DEFAULT currency's, as when listing (see `Product.create`), and the
 * new shape has no stock until it is set below.
 * </p>
 */
export function AddVariantForm({ product }: { product: Product }) {
  const { t } = useTranslation('seller')
  const add = useAddVariant(product.id)
  const names = [...new Set((product.variants ?? []).flatMap((variant) => variant.options.map((option) => option.name)))]
  // A product sold one way has no options, and a second shape needs one to tell it apart - the server refuses a second
  // variant with none (409) - so the form starts with an empty option there.
  const blank = () => ({
    sku: '',
    price: '',
    options: names.length > 0 ? names.map((name) => ({ name, value: '' })) : [{ name: '', value: '' }],
  })
  const [form, setForm] = useState(blank)

  const options = form.options.filter((option) => option.name.trim() && option.value.trim())
  const ready = form.sku.trim() !== '' && Number(form.price) > 0

  function submit(event: React.FormEvent) {
    event.preventDefault()
    add.mutate(
      {
        sku: form.sku.trim(),
        price: Number(form.price),
        options: options.map((option) => ({ name: option.name.trim(), value: option.value.trim() })),
      },
      {
        onSuccess: () => {
          toast.success(t('addVariant.added', { sku: form.sku.trim() }))
          setForm(blank())
        },
      },
    )
  }

  const setOption = (index: number, field: 'name' | 'value', value: string) =>
    setForm({ ...form, options: form.options.map((option, i) => (i === index ? { ...option, [field]: value } : option)) })

  const priceLabel = t('addVariant.price', { currency: DEFAULT_CURRENCY })

  return (
    <form onSubmit={submit} className="bg-secondary/40 grid gap-4 rounded-2xl p-4" aria-label={t('addVariant.title')}>
      <div>
        <h3 className="font-semibold">{t('addVariant.title')}</h3>
        <p className="text-muted-foreground text-sm">{t('addVariant.hint')}</p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="variant-sku">{t('create.sku')}</Label>
          <Input
            id="variant-sku"
            required
            maxLength={50}
            className="h-10 rounded-xl font-mono"
            value={form.sku}
            onChange={(event) => setForm({ ...form, sku: event.target.value })}
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="variant-price">{priceLabel}</Label>
          <InputGroup className="h-10 rounded-xl">
            <InputGroupInput
              id="variant-price"
              required
              inputMode="decimal"
              value={form.price}
              onChange={(event) => setForm({ ...form, price: event.target.value })}
            />
            <InputGroupAddon align="inline-end">
              <InputGroupText>{DEFAULT_CURRENCY}</InputGroupText>
            </InputGroupAddon>
          </InputGroup>
        </div>
      </div>

      <fieldset className="grid gap-2">
        <legend className="mb-2 text-sm font-medium">{t('addVariant.options')}</legend>
        {names.length === 0 && <p className="text-muted-foreground text-xs">{t('addVariant.firstOption')}</p>}
        {form.options.map((option, index) => (
          <div key={index} className="grid grid-cols-[1fr_1fr_auto] items-center gap-2">
            <Input
              aria-label={t('addVariant.optionName', { number: index + 1 })}
              placeholder={t('addVariant.optionNamePlaceholder')}
              maxLength={50}
              className="h-9 rounded-xl"
              value={option.name}
              onChange={(event) => setOption(index, 'name', event.target.value)}
            />
            <Input
              aria-label={t('addVariant.optionValue', { number: index + 1 })}
              placeholder={t('addVariant.optionValuePlaceholder')}
              maxLength={100}
              className="h-9 rounded-xl"
              value={option.value}
              onChange={(event) => setOption(index, 'value', event.target.value)}
            />
            <Button
              type="button"
              size="icon-sm"
              variant="ghost"
              className="rounded-full"
              aria-label={t('addVariant.removeOption', { number: index + 1 })}
              onClick={() => setForm({ ...form, options: form.options.filter((_, i) => i !== index) })}
            >
              <XIcon />
            </Button>
          </div>
        ))}
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="w-fit rounded-full"
          onClick={() => setForm({ ...form, options: [...form.options, { name: '', value: '' }] })}
        >
          <PlusIcon /> {t('addVariant.addOption')}
        </Button>
      </fieldset>

      <ServerError error={add.error} fallback={t('listing.loadFailed')} />
      <Button type="submit" className="w-fit rounded-full" disabled={!ready || add.isPending}>
        <PlusIcon /> {t('addVariant.submit')}
      </Button>
    </form>
  )
}
