import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { GlobeIcon } from 'lucide-react'
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
import { LANGUAGES, currentLanguage, type Language } from '@/config/i18n'

/**
 * Which language the shop speaks to this visitor (specs/021).
 *
 * A menu behind a globe rather than a full-width select: it is set once and then left alone, so it
 * earns an icon's worth of the bar, not a field's. The trigger still says which language is on, so
 * nobody has to open it to find out.
 *
 * Changing it re-reads everything from the server as well as re-rendering the interface: product names
 * and option values are Catalog's text, not the storefront's, and they arrive in whichever language
 * the request asked for.
 */
export function LanguageSwitcher() {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const language = currentLanguage()

  async function choose(next: Language) {
    await i18n.changeLanguage(next)
    // Every cached answer was fetched in the old language.
    await queryClient.invalidateQueries()
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="sm" className="h-9 gap-1.5 rounded-full px-3" />}
        aria-label={t('language.label')}
      >
        <GlobeIcon className="size-4" />
        <span className="text-xs font-semibold uppercase">{language}</span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44">
        <DropdownMenuGroup>
          <DropdownMenuLabel>{t('language.label')}</DropdownMenuLabel>
          <DropdownMenuRadioGroup value={language} onValueChange={(value) => void choose(value as Language)}>
            {LANGUAGES.map((tag) => (
              <DropdownMenuRadioItem key={tag} value={tag}>
                {t(`language.${tag}`)}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
