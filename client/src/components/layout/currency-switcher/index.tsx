import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { CURRENCIES, currentCurrency, setCurrentCurrency, type Currency } from '@/config/money'

/**
 * Which currency this visitor is shown prices in (specs/022).
 *
 * **Deliberately a separate control from the language one.** They look alike and are not the same
 * choice: a Vietnamese person reading English still pays in dong. Putting them in one control would
 * take one of the two choices away from whoever it is wrong for.
 *
 * Changing it re-reads everything from the server, like the language switcher does, and for a
 * stronger reason: every cached answer holds amounts in the old currency, and a price is the thing in
 * this shop most likely to be believed.
 */
export function CurrencySwitcher() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  // Held in state as well as in storage, because reading storage does not re-render.
  const [currency, setCurrency] = useState<Currency>(currentCurrency)

  const items = Object.fromEntries(CURRENCIES.map((code) => [code, t(`currency.${code}`)]))

  async function choose(next: Currency) {
    setCurrentCurrency(next)
    setCurrency(next)
    await queryClient.invalidateQueries()
  }

  return (
    <Select
      items={items}
      value={currency}
      onValueChange={(value) => value && void choose(String(value) as Currency)}
    >
      <SelectTrigger size="sm" className="w-28" aria-label={t('currency.label')}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {CURRENCIES.map((code) => (
          <SelectItem key={code} value={code}>
            {t(`currency.${code}`)}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
