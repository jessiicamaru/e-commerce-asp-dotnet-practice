import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { BanknoteIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
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

  async function choose(next: Currency) {
    setCurrentCurrency(next)
    setCurrency(next)
    await queryClient.invalidateQueries()
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="sm" className="h-9 gap-1.5 rounded-full px-3" />}
        aria-label={t('currency.label')}
      >
        <BanknoteIcon className="size-4" />
        <span className="text-xs font-semibold">{currency}</span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44">
        <DropdownMenuGroup>
          <DropdownMenuLabel>{t('currency.label')}</DropdownMenuLabel>
          <DropdownMenuRadioGroup value={currency} onValueChange={(value) => void choose(value as Currency)}>
            {CURRENCIES.map((code) => (
              <DropdownMenuRadioItem key={code} value={code}>
                {t(`currency.${code}`)}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
