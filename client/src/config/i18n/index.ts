import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'

import viAuth from '@/locales/vi/auth.json'
import viCart from '@/locales/vi/cart.json'
import viCatalog from '@/locales/vi/catalog.json'
import viCheckout from '@/locales/vi/checkout.json'
import viCommon from '@/locales/vi/common.json'
import viOrders from '@/locales/vi/orders.json'
import viSeller from '@/locales/vi/seller.json'
import viAdmin from '@/locales/vi/admin.json'
import viNotifications from '@/locales/vi/notifications.json'
import viStatus from '@/locales/vi/status.json'

import enAuth from '@/locales/en/auth.json'
import enCart from '@/locales/en/cart.json'
import enCatalog from '@/locales/en/catalog.json'
import enCheckout from '@/locales/en/checkout.json'
import enCommon from '@/locales/en/common.json'
import enOrders from '@/locales/en/orders.json'
import enSeller from '@/locales/en/seller.json'
import enAdmin from '@/locales/en/admin.json'
import enNotifications from '@/locales/en/notifications.json'
import enStatus from '@/locales/en/status.json'

/** The languages this shop speaks. The server's list has to match (specs/021). */
export const LANGUAGES = ['vi', 'en'] as const

export type Language = (typeof LANGUAGES)[number]

export const DEFAULT_LANGUAGE: Language = 'vi'

/** Where the visitor's choice is kept, and what the axios layer reads to set Accept-Language. */
export const LANGUAGE_STORAGE_KEY = 'language'

/**
 * The interface's half of speaking two languages (specs/021).
 *
 * The product text is the server's half: it is translated in Catalog and arrives already in the right
 * language, because every call carries `Accept-Language`. The two agree because both read the same
 * stored choice.
 *
 * Plurals are i18next's, not a hand-written `s`: "1 sản phẩm" and "2 sản phẩm" happen to be the same
 * word in Vietnamese and are not in English, and that is exactly the difference a plural rule exists
 * to know.
 */
void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      vi: {
        common: viCommon,
        catalog: viCatalog,
        cart: viCart,
        checkout: viCheckout,
        orders: viOrders,
        seller: viSeller,
        admin: viAdmin,
        notifications: viNotifications,
        auth: viAuth,
        status: viStatus,
      },
      en: {
        common: enCommon,
        catalog: enCatalog,
        cart: enCart,
        checkout: enCheckout,
        orders: enOrders,
        seller: enSeller,
        admin: enAdmin,
        notifications: enNotifications,
        auth: enAuth,
        status: enStatus,
      },
    },
    supportedLngs: LANGUAGES,
    fallbackLng: DEFAULT_LANGUAGE,
    // A key with no Vietnamese shows the English text rather than "catalog.addToCart" (FR-002).
    fallbackNS: 'common',
    defaultNS: 'common',
    ns: ['common', 'catalog', 'cart', 'checkout', 'orders', 'auth', 'status'],
    detection: {
      // The stored choice first, then what the browser asks for - the same order the server uses.
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: LANGUAGE_STORAGE_KEY,
      caches: ['localStorage'],
    },
    interpolation: {
      // React escapes what it renders; i18next escaping again would show &#39; to the customer.
      escapeValue: false,
    },
  })

export default i18n

/** The tag the app is in right now, reduced to one the shop speaks. */
export function currentLanguage(): Language {
  const candidate = (i18n.resolvedLanguage ?? i18n.language ?? DEFAULT_LANGUAGE).split('-')[0]
  return LANGUAGES.includes(candidate as Language) ? (candidate as Language) : DEFAULT_LANGUAGE
}
