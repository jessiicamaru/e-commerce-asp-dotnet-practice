import { useTranslation } from 'react-i18next'
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from '@/components/ui/combobox'
import { cn, looselyIncludes, type Choice } from '@/utils/shared'

/**
 * A dropdown you can type into, for lists long enough to scroll: categories, countries.
 *
 * <p>
 * Nothing but shadcn's Combobox, assembled once so the three places that need it look and behave
 * alike. Matching ignores accents and case, so "may anh" finds "Máy ảnh" - the same promise the
 * catalogue search makes on the server (specs/021).
 * </p>
 * <p>
 * The value is a plain string on the way in and out; the object the Combobox wants is looked up here,
 * so a caller never has to keep two representations of one choice in sync.
 * </p>
 */
export function SearchableSelect({
  id,
  choices,
  value,
  onChange,
  placeholder,
  label,
  required,
  className,
}: {
  id?: string
  choices: Choice[]
  value: string | null
  onChange: (value: string | null) => void
  placeholder?: string
  /** The accessible name, when there is no visible <Label htmlFor>. */
  label?: string
  required?: boolean
  className?: string
}) {
  const { t } = useTranslation()
  const selected = choices.find((choice) => choice.value === value) ?? null

  return (
    <Combobox<Choice>
      items={choices}
      value={selected}
      onValueChange={(next) => onChange(next?.value ?? null)}
      itemToStringLabel={(choice) => choice.label}
      isItemEqualToValue={(a, b) => a.value === b.value}
      filter={(choice, query) => looselyIncludes(`${choice.label} ${choice.hint ?? ''}`, query)}
      required={required}
    >
      <ComboboxInput
        id={id}
        aria-label={label}
        placeholder={placeholder}
        // Everything selected on focus, so typing REPLACES the chosen label. Without this a click put
        // the caret mid-word and "ong kinh" was typed into the middle of "Tất cả danh mục" - which
        // matched nothing. Seen in a screenshot.
        onFocus={(event) => event.currentTarget.select()}
        className={cn('h-10 w-full rounded-xl', className)}
      />
      <ComboboxContent>
        <ComboboxEmpty>{t('searchable.none')}</ComboboxEmpty>
        <ComboboxList>
          {(choice: Choice) => (
            <ComboboxItem key={choice.value} value={choice}>
              <span className="grid">
                <span>{choice.label}</span>
                {choice.hint && <span className="text-muted-foreground text-xs">{choice.hint}</span>}
              </span>
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}
