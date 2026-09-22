import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { LANGUAGES, currentLanguage, type Language } from '@/config/i18n'

/**
 * Which language the shop speaks to this visitor (specs/021).
 *
 * Changing it re-reads everything from the server as well as re-rendering the interface: product names
 * and option values are Catalog's text, not the storefront's, and they arrive in whichever language
 * the request asked for.
 */
export function LanguageSwitcher() {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const language = currentLanguage()

  const items = Object.fromEntries(LANGUAGES.map((tag) => [tag, t(`language.${tag}`)]))

  async function choose(next: Language) {
    await i18n.changeLanguage(next)
    // Every cached answer was fetched in the old language.
    await queryClient.invalidateQueries()
  }

  return (
    <Select items={items} value={language} onValueChange={(value) => value && void choose(String(value) as Language)}>
      <SelectTrigger size="sm" className="w-32" aria-label={t('language.label')}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {LANGUAGES.map((tag) => (
          <SelectItem key={tag} value={tag}>
            {t(`language.${tag}`)}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
