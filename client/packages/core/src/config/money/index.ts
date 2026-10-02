/**
 * The currencies this shop prices in, and the visitor's choice of one (specs/022).
 *
 * **Separate from the language on purpose.** Most of this shop's customers are Vietnamese people
 * reading English, and they pay in dong; a visitor reading Vietnamese may still want dollars.
 * Language is what you read, currency is what you pay, and tying them together takes one of the two
 * choices away from whoever it is wrong for.
 *
 * **Nothing here converts.** The amount shown is the amount the server sent, which is the amount an
 * administrator typed for that currency. A variant nobody priced in the chosen currency arrives with
 * `price: null`, and the interface says so rather than showing a number it made up.
 */
export const CURRENCIES = ['VND', 'USD'] as const

export type Currency = (typeof CURRENCIES)[number]

/** What the shop prices in when nobody has chosen. The server's default has to match. */
export const DEFAULT_CURRENCY: Currency = 'VND'

/** Where the visitor's choice is kept, and what the axios layer reads to set `X-Currency`. */
export const CURRENCY_STORAGE_KEY = 'currency'

/**
 * There is no standard request header for a currency - a currency is a property of the offer, not of
 * the representation - so this is a plainly non-standard one rather than a standard one given an
 * invented meaning.
 */
export const CURRENCY_HEADER = 'X-Currency'

function isCurrency(value: string | null): value is Currency {
  return value !== null && (CURRENCIES as readonly string[]).includes(value)
}

/** The currency the shop is being read in right now. */
export function currentCurrency(): Currency {
  try {
    const stored = localStorage.getItem(CURRENCY_STORAGE_KEY)
    return isCurrency(stored) ? stored : DEFAULT_CURRENCY
  } catch {
    // Private browsing, blocked site data: the default is a working shop, not an error.
    return DEFAULT_CURRENCY
  }
}

/** Remembers the choice. The caller is responsible for refetching - every price was in the old one. */
export function setCurrentCurrency(currency: Currency) {
  try {
    localStorage.setItem(CURRENCY_STORAGE_KEY, currency)
  } catch {
    // Not being able to remember it is worth less than not being able to change it.
  }
}
