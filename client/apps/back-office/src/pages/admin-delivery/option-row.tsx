import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Button } from '@ecommerce/ui/button'
import { Input } from '@ecommerce/ui/input'
import { Label } from '@ecommerce/ui/label'
import { ApiError } from '@ecommerce/core/config/axios'
import { CURRENCIES } from '@ecommerce/core/config/money'
import type { useDeliveryChanges } from '@ecommerce/core/hooks/delivery'
import type { DeliveryOption } from '@ecommerce/core/services/delivery/types'

/**
 * One delivery option, edited in place - or, with `option` null, a new one under a code of the administrator's choosing.
 * A currency left empty is one the option is not offered in (specs/022); the code is fixed once saved.
 */
export function OptionRow({ option, changes }: { option: DeliveryOption | null; changes: ReturnType<typeof useDeliveryChanges> }) {
  const { t } = useTranslation('admin')
  const id = option?.code ?? 'new'
  const [code, setCode] = useState(option?.code ?? '')
  const [name, setName] = useState(option?.name ?? '')
  const [active, setActive] = useState(option?.isActive ?? true)
  const [sortOrder, setSortOrder] = useState(String(option?.sortOrder ?? 10))
  const [prices, setPrices] = useState<Record<string, string>>(
    Object.fromEntries(CURRENCIES.map((c) => [c, option?.prices[c] !== undefined ? String(option.prices[c]) : ''])),
  )
  // How long it takes, in business days (specs/134): both or neither - empty says nothing at checkout.
  const [minDays, setMinDays] = useState(option?.minDays != null ? String(option.minDays) : '')
  const [maxDays, setMaxDays] = useState(option?.maxDays != null ? String(option.maxDays) : '')
  const [refusal, setRefusal] = useState<string | null>(null)

  const save = (event: FormEvent) => {
    event.preventDefault()
    setRefusal(null)
    const priced = Object.fromEntries(
      Object.entries(prices)
        .filter(([, amount]) => amount.trim() !== '')
        .map(([currency, amount]) => [currency, Number(amount)]),
    )
    changes.option
      .mutateAsync({
        code: code.trim(),
        name: name.trim(),
        isActive: active,
        sortOrder: Number(sortOrder) || 0,
        prices: priced,
        minDays: minDays.trim() === '' ? null : Number(minDays),
        maxDays: maxDays.trim() === '' ? null : Number(maxDays),
      })
      .then(() => {
        toast.success(t('deliverySettings.optionSaved', { name: name.trim() }))
        if (!option) {
          setCode('')
          setName('')
        }
      }, (error) => setRefusal(ApiError.from(error).message))
  }

  return (
    <li className="bg-card ring-border/60 rounded-3xl p-4 ring-1">
      <form onSubmit={save} className="grid gap-3" aria-label={option ? option.name : t('deliverySettings.newOption')}>
        <div className="grid gap-3 sm:grid-cols-[10rem_1fr_6rem]">
          <div className="grid gap-1.5">
            <Label htmlFor={`${id}-code`}>{t('deliverySettings.code')}</Label>
            <Input id={`${id}-code`} value={code} onChange={(e) => setCode(e.target.value)} readOnly={!!option} required maxLength={32} />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor={`${id}-name`}>{t('deliverySettings.name')}</Label>
            <Input id={`${id}-name`} value={name} onChange={(e) => setName(e.target.value)} required maxLength={100} />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor={`${id}-order`}>{t('deliverySettings.order')}</Label>
            <Input id={`${id}-order`} type="number" min={0} max={1000} value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
          </div>
        </div>
        <div className="flex flex-wrap items-end gap-3">
          {CURRENCIES.map((currency) => (
            <div key={currency} className="grid gap-1.5">
              <Label htmlFor={`${id}-price-${currency}`}>{t('deliverySettings.priceIn', { currency })}</Label>
              <Input
                id={`${id}-price-${currency}`}
                type="number"
                min={0}
                step="any"
                value={prices[currency]}
                onChange={(e) => setPrices((p) => ({ ...p, [currency]: e.target.value }))}
                className="w-36"
              />
            </div>
          ))}
          <div className="grid gap-1.5">
            <Label htmlFor={`${id}-min-days`}>{t('deliverySettings.minDays')}</Label>
            <Input id={`${id}-min-days`} type="number" min={0} max={60} value={minDays} onChange={(e) => setMinDays(e.target.value)} className="w-28" />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor={`${id}-max-days`}>{t('deliverySettings.maxDays')}</Label>
            <Input id={`${id}-max-days`} type="number" min={0} max={60} value={maxDays} onChange={(e) => setMaxDays(e.target.value)} className="w-28" />
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} />
            {t('deliverySettings.offered')}
          </label>
          <Button type="submit" className="rounded-full" disabled={!code.trim() || !name.trim() || changes.option.isPending}>
            {option ? t('deliverySettings.save') : t('deliverySettings.add')}
          </Button>
        </div>
        {refusal && <p className="text-destructive text-sm">{refusal}</p>}
      </form>
    </li>
  )
}
