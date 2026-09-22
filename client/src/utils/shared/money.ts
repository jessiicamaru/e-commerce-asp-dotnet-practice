import { currentLanguage } from '@/config/i18n'
import { CURRENCIES, DEFAULT_CURRENCY, currentCurrency } from '@/config/money'

/**
 * An amount of money, formatted for the person reading it - never computed with.
 *
 * **Two inputs, and they are not the same input** (specs/022). The language decides the separators
 * and where the symbol goes; the currency decides which symbol and how many decimal places.
 * `40.000.000 ₫` in Vietnamese and `₫40,000,000` in English are the same amount, and each is right
 * for its reader. `Intl` knows every currency's decimals already, so there is no table here to keep
 * in step with the server's - dong simply comes out with none.
 *
 * Nothing here converts. An amount arrives in a currency the server chose and is shown in it.
 *
 * A code the storefront does not know falls back to the default rather than throwing: the server
 * having currencies this build has not heard of is a deployment being half-finished, not a reason to
 * blank the page.
 */
export const money = (value: number, currency: string = currentCurrency()) => {
  const code = (CURRENCIES as readonly string[]).includes(currency) ? currency : DEFAULT_CURRENCY

  return value.toLocaleString(currentLanguage(), { style: 'currency', currency: code })
}
